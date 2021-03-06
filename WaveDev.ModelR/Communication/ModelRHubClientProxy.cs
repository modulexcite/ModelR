﻿using System;
using System.Diagnostics;
using Microsoft.AspNet.SignalR.Client;
using System.Collections.Generic;
using Microsoft.AspNet.SignalR.Client.Transports;
using SharpGL.SceneGraph.Primitives;
using SharpGL.SceneGraph.Quadrics;
using WaveDev.ModelR.Shared.Models;
using System.Globalization;
using WaveDev.ModelR.ViewModels;
using Xceed.Wpf.Toolkit;
using WaveDev.ModelR.Shared;
using System.IO;
using System.Threading.Tasks;
using System.Net;

namespace WaveDev.ModelR.Communication
{
    internal class ModelRHubClientProxy : IDisposable
    {
        #region Private Fields

        private static ModelRHubClientProxy s_instance;

        private HubConnection _connection;
        private IHubProxy _proxy;
        private IEnumerable<SceneInfoModel> _cachedScenes;
        private Guid _sceneId;

        #endregion

        #region Delegates

        public delegate void SceneObjectCreatedEventHandler(SceneObjectInfoModel infoModel);
        public delegate void SceneObjectTransformedEventHandler(SceneObjectInfoModel infoModel);
        public delegate void UserJoinedEventHandler(UserInfoModel infoModel);
        public delegate void UserLoggedOffEventHandler(UserInfoModel infoModel);

        #endregion

        #region Events

        public event SceneObjectCreatedEventHandler SceneObjectCreated;
        public event SceneObjectTransformedEventHandler SceneObjectTransformed;
        public event UserJoinedEventHandler UserJoined;
        public event UserLoggedOffEventHandler UserLoggedOff;

        #endregion

        #region Static Members

        /// <summary>
        /// This method returns an instance of this proxy class that can be used by clients to communicate with the server.
        /// </summary>
        /// <param name="url">The url of the SignalR server.</param>
        /// <param name="createIfNotExist">
        /// Per default: if no instance exists, a new instance is created, cached and returned. That can be omitted, if
        /// parameter createIfNotExist is set to false. In that case, null is returned if not instance exists. Can be used 
        /// to check for instance.
        /// </param>
        /// <returns>Returns an instance of this proxy class.</returns>
        public static ModelRHubClientProxy GetInstance(string url = Constants.ModelRServerUrl, bool createIfNotExist = true)
        {
            var urlChanged = s_instance!=null && String.Compare(s_instance.ServerUrl, url, StringComparison.Ordinal) != 0;

            if ((s_instance == null || urlChanged) && createIfNotExist)
                s_instance = new ModelRHubClientProxy(url);

            return s_instance;
        }

        #endregion

        #region Construction

        private ModelRHubClientProxy(string url)
        {
            ServerUrl = url;

            ConnectToServer();
        }

        #endregion

        #region IDisposable 

        public void Dispose()
        {
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }

            _proxy = null;
        }

        #endregion

        #region Public Members

        public string LoggedInUserName
        {
            get;
            set;
        }

        public string ServerUrl
        {
            get;
            set;
        }
        
        public void Login(string user, string password, Guid sceneId)
        {
            try
            {
                _connection.Stop();

                _connection.Credentials = new NetworkCredential(user, password);

                // [RS] The authentication token(s) should be encrypted. Sending the user name and the password in clear text here
                //      is just for demonatration how authorization can be implemented.
                _connection.Headers.Add("ModelRAuthToken_UserName", user);
                _connection.Headers.Add("ModelRAuthToken_UserPassword", password);

                _connection.Start().Wait();

                // [RS] Don't do it async, because we have to wait if user is authorized to join the scene. If not,
                //      the UserNotAuthorizedException will be thrown. The client code has to shutdown the application.
                var userModel = _proxy.Invoke<UserInfoModel>("Login", sceneId).Result;

                _sceneId = sceneId;

                LoggedInUserName = user;
            }
            catch
            {
                throw new UserNotAuthorizedException(string.Format(CultureInfo.CurrentUICulture, "The user '{0}' is not authorized or known in the system.", user), user);
            }
        }

        public void Logoff()
        {
            if (_connection != null)
            {
                // Logoff at server.
                if (_proxy != null)
                    _proxy.Invoke("Logoff");

                _connection.Stop();

                // [RS] Remove authentification information from connection in order it is reused. Requires ´new login.
                //      Windows Authentication
                _connection.Credentials = null;
                //      Header Authentication
                _connection.Headers.Remove("ModelRAuthToken_UserName");
                _connection.Headers.Remove("ModelRAuthToken_UserPassword");
                //      Cookie Authentication
                _connection.CookieContainer = null;
            }

            LoggedInUserName = string.Empty;
        }

        public IEnumerable<SceneInfoModel> Scenes
        {
            get
            {
                if (_cachedScenes == null)
                    _cachedScenes = _proxy.Invoke<IEnumerable<SceneInfoModel>>("GetAvailableScenes").Result;

                return _cachedScenes;
            }
        }

        public async Task CreateSceneObject(ObjectModel sceneObject)
        {
            var type = SceneObjectType.Light;

            if (sceneObject.SceneElement is Teapot)
                type = SceneObjectType.Teapot;
            else if (sceneObject.SceneElement is Cube)
                type = SceneObjectType.Cube;
            else if (sceneObject.SceneElement is Cylinder)
                type = SceneObjectType.Cylinder;
            else if (sceneObject.SceneElement is Disk)
                type = SceneObjectType.Disk;
            else if (sceneObject.SceneElement is Sphere)
                type = SceneObjectType.Sphere;

            var infoModel = new SceneObjectInfoModel(sceneObject.Id, _sceneId) { SceneObjectType = type };

            try
            {
                await _proxy.Invoke("CreateSceneObject", infoModel);
            }
            catch (InvalidOperationException exception)
            {
                throw new UserNotAuthorizedException(string.Format(CultureInfo.CurrentUICulture, "The user '{0}' is not authorized or known in the system.", LoggedInUserName), LoggedInUserName);
            }
        }

        public async Task TransformSceneObject(ObjectModel sceneObject)
        {
            var infoModel = new SceneObjectInfoModel(sceneObject.Id, _sceneId);

            infoModel.Transformation = new TransformationInfoModel()
            {
                TranslateX = sceneObject.Transformation.TranslateX,
                TranslateY = sceneObject.Transformation.TranslateY,
                TranslateZ = sceneObject.Transformation.TranslateZ,
                RotateX = sceneObject.Transformation.RotateX,
                RotateY = sceneObject.Transformation.RotateY,
                RotateZ = sceneObject.Transformation.RotateZ,
                ScaleX = sceneObject.Transformation.ScaleX,
                ScaleY = sceneObject.Transformation.ScaleY,
                ScaleZ = sceneObject.Transformation.ScaleZ
            };

            try
            {
                await _proxy.Invoke("TransformSceneObject", infoModel);
            }
            catch (InvalidOperationException exception)
            {
                throw new UserNotAuthorizedException(string.Format(CultureInfo.CurrentUICulture, "The user '{0}' is not authorized or known in the system.", LoggedInUserName), LoggedInUserName);
            }
        }

        public async Task<IEnumerable<UserInfoModel>> GetUsers()
        {
            try
            {
                return await _proxy.Invoke<IEnumerable<UserInfoModel>>("GetUsers");
            }
            catch (InvalidOperationException exception)
            {
                throw new UserNotAuthorizedException(string.Format(CultureInfo.CurrentUICulture, "The user '{0}' is not authorized or known in the system.", LoggedInUserName), LoggedInUserName);
            }
        }

        public async Task<IEnumerable<SceneObjectInfoModel>> GetSceneObjects()
        {
            try
            {
                return await _proxy.Invoke<IEnumerable<SceneObjectInfoModel>>("GetSceneObjects");
            }
            catch (InvalidOperationException exception)
            {
                throw new UserNotAuthorizedException(string.Format(CultureInfo.CurrentUICulture, "The user '{0}' is not authorized or known in the system.", LoggedInUserName), LoggedInUserName);
            }
        }

        #endregion

        #region Private Methods

        private void ConnectToServer()
        {
            try
            {
                var logWriter = new StreamWriter(@"..\..\..\Logs\ModelR.Client." + Guid.NewGuid().ToString() + ".log") { AutoFlush = true };

                _connection = new HubConnection(ServerUrl)
                {
                    TraceLevel = TraceLevels.All,
                    TraceWriter = logWriter
                };

                _proxy = _connection.CreateHubProxy(Constants.ModelRHubName);

                _proxy.On<SceneObjectInfoModel>("SceneObjectCreated", infoModel => OnSceneObjectCreated(infoModel));
                _proxy.On<SceneObjectInfoModel>("SceneObjectTransformed", infoModel => OnSceneObjectTransformed(infoModel));
                _proxy.On<UserInfoModel>("UserJoined", infoModel => OnUserJoined(infoModel));
                _proxy.On<UserInfoModel>("UserLoggedOf", infoModel => OnUserLoggedOff(infoModel));
                
                // TODO: [RS] Method cannot be async here, because it is called from the construtor.
                _connection.Start().Wait();
            }
            catch (AggregateException exception)
            {
                Exception nextException;
                var error = exception.Message;

                foreach (var innerException in exception.InnerExceptions)
                {
                    nextException = innerException;

                    while (nextException != null)
                    {
                        error = string.Format(CultureInfo.CurrentUICulture, "{0}{1}{2}", error, Environment.NewLine, nextException.Message);
                        nextException = nextException.InnerException;
                    }

                }

                throw new InvalidOperationException(error, exception);
            }
        }

        #region Event Raise Helper for SignalR Client Method Calls

        private void OnUserJoined(UserInfoModel infoModel)
        {
            if (UserJoined != null)
                UserJoined(infoModel);
        }

        private void OnUserLoggedOff(UserInfoModel infoModel)
        {
            if (UserLoggedOff != null)
                UserLoggedOff(infoModel);
        }

        private void OnSceneObjectTransformed(SceneObjectInfoModel infoModel)
        {
            if (SceneObjectTransformed != null)
                SceneObjectTransformed(infoModel);
        }

        private void OnSceneObjectCreated(SceneObjectInfoModel infoModel)
        {
            if (SceneObjectCreated != null)
                SceneObjectCreated(infoModel);
        }

        #endregion

        #endregion

    }
}
