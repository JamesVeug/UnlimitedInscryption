using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace UnlimitedInscryption
{
    public static class Utils
    {
        public static Texture2D GetTextureFromPath(string path)
        {
            byte[] imgBytes = File.ReadAllBytes(Path.Combine(Plugin.PluginDirectory, path));
            Texture2D tex = new Texture2D(2,2);
            tex.LoadImage(imgBytes);

            /*if (!TextureToPath.TryGetValue(tex, out _))
            {
                TextureToPath[tex] = path;
            }*/

            return tex;
        }
        
        public static void PrintHierarchy(GameObject go, bool printParents)
        {
            string prefix = "";
            if (printParents)
            {
                List<Transform> hierarchy = new List<Transform>();
                
                Transform t = go.transform.parent;
                while (t != null)
                {
                    hierarchy.Add(t);
                    t = t.parent;
                }

                for (int i = hierarchy.Count - 1; i >= 0; i--)
                {
                    Transform tran = hierarchy[i];
                    string text = prefix + tran.gameObject.name + "(" + tran.gameObject.GetInstanceID() + ")";
                    Plugin.Log.LogInfo(prefix + text);

                    prefix += "\t";
                }
            }

            PrintGameObject(go, prefix);
        }

        private static void PrintGameObject(GameObject go, string prefix = "")
        {
            string text = go.name + "(" + go.GetInstanceID() + ")";
            Plugin.Log.LogInfo(prefix + text);
            Plugin.Log.LogInfo(prefix + "- Components: " + go.transform.childCount);
            foreach (Component component in go.GetComponents<Component>())
            {
                Plugin.Log.LogInfo(prefix + "-- " + component.GetType());
                if (component is SpriteRenderer spriteRenderer)
                {
                    Plugin.Log.LogInfo(prefix + "-- Name: " + spriteRenderer.name);
                    Plugin.Log.LogInfo(prefix + "-- Sprite Name: " + (spriteRenderer.sprite != null ? spriteRenderer.sprite.name : "null"));
                }
            }

            Plugin.Log.LogInfo(prefix + "- Children: " + go.transform.childCount);
            for (int i = 0; i < go.transform.childCount; i++)
            {
                PrintGameObject(go.transform.GetChild(i).gameObject, prefix + "\t");
            }
        }
        
        /// <summary>
        /// Returns a _private_ Property Value from a given Object. Uses Reflection.
        /// Throws a ArgumentOutOfRangeException if the Property is not found.
        /// </summary>
        /// <typeparam name="T">Type of the Property</typeparam>
        /// <param name="obj">Object from where the Property Value is returned</param>
        /// <param name="propName">Propertyname as string.</param>
        /// <returns>PropertyValue</returns>
        public static T GetPrivatePropertyValue<T>(this object obj, string propName)
        {
            if (obj == null) throw new ArgumentNullException("obj");
            PropertyInfo pi = obj.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (pi == null) throw new ArgumentOutOfRangeException("propName", string.Format("Property {0} was not found in Type {1}", propName, obj.GetType().FullName));
            return (T)pi.GetValue(obj, null);
        }

        /// <summary>
        /// Returns a private Property Value from a given Object. Uses Reflection.
        /// Throws a ArgumentOutOfRangeException if the Property is not found.
        /// </summary>
        /// <typeparam name="T">Type of the Property</typeparam>
        /// <param name="obj">Object from where the Property Value is returned</param>
        /// <param name="propName">Propertyname as string.</param>
        /// <returns>PropertyValue</returns>
        public static T GetPrivateFieldValue<T>(this object obj, string propName)
        {
            if (obj == null) throw new ArgumentNullException("obj");
            Type t = obj.GetType();
            FieldInfo fi = null;
            while (fi == null && t != null)
            {
                fi = t.GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                t = t.BaseType;
            }
            if (fi == null) throw new ArgumentOutOfRangeException("propName", string.Format("Field {0} was not found in Type {1}", propName, obj.GetType().FullName));
            return (T)fi.GetValue(obj);
        }

        /// <summary>
        /// Sets a _private_ Property Value from a given Object. Uses Reflection.
        /// Throws a ArgumentOutOfRangeException if the Property is not found.
        /// </summary>
        /// <typeparam name="T">Type of the Property</typeparam>
        /// <param name="obj">Object from where the Property Value is set</param>
        /// <param name="propName">Propertyname as string.</param>
        /// <param name="val">Value to set.</param>
        /// <returns>PropertyValue</returns>
        public static void SetPrivatePropertyValue<T>(this object obj, string propName, T val)
        {
            Type t = obj.GetType();
            if (t.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) == null)
                throw new ArgumentOutOfRangeException("propName", string.Format("Property {0} was not found in Type {1}", propName, obj.GetType().FullName));
            t.InvokeMember(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance, null, obj, new object[] { val });
        }

        /// <summary>
        /// Set a private Property Value on a given Object. Uses Reflection.
        /// </summary>
        /// <typeparam name="T">Type of the Property</typeparam>
        /// <param name="obj">Object from where the Property Value is returned</param>
        /// <param name="propName">Propertyname as string.</param>
        /// <param name="val">the value to set</param>
        /// <exception cref="ArgumentOutOfRangeException">if the Property is not found</exception>
        public static void SetPrivateFieldValue<T>(this object obj, string propName, T val)
        {
            if (obj == null) throw new ArgumentNullException("obj");
            Type t = obj.GetType();
            FieldInfo fi = null;
            while (fi == null && t != null)
            {
                fi = t.GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                t = t.BaseType;
            }
            if (fi == null) throw new ArgumentOutOfRangeException("propName", string.Format("Field {0} was not found in Type {1}", propName, obj.GetType().FullName));
            fi.SetValue(obj, val);
        }
    }
}