﻿using System;
using System.Windows.Media.Imaging;
using SharpGL.SceneGraph.Core;
using SharpGL.SceneGraph.Primitives;

namespace WaveDev.ModelR.ViewModels
{
    public class ObjectModel
    {
        public ObjectModel(SceneElement sceneElement)
        {
            if (sceneElement == null)
                throw new ArgumentNullException("sceneElement");

            SceneElement = sceneElement;
        }

        public SceneElement SceneElement
        {
            get;
            set;
        }

        public string Name
        {
            get
            {
                return SceneElement.Name;
            }

            set
            {
                SceneElement.Name = value;
            }
        }

        public BitmapImage Image
        {
            get
            {
                BitmapImage image = null;

                if (SceneElement is Cube)
                {
                    var uri = new Uri("/WaveDev.ModelR;component/Images/Cube.png", UriKind.Relative);
                    image = new BitmapImage(uri);
                }
                else if (SceneElement is Teapot)
                {
                    var uri = new Uri("/WaveDev.ModelR;component/Images/Teapot.png", UriKind.Relative);
                    image = new BitmapImage(uri);
                }

                return image;
            }
        }

    }
}