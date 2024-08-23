using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tan_Art
{
    public abstract class Singleton<T> where T : class
    {
        private static T instance = null;
        private static readonly object g_instanceLock = new object();

        public static bool hasInstance
        {
            get { return instance != null; }
        }

        public static T Ins
        {
            get
            {
                if (instance == null)
                {
                    lock (g_instanceLock)
                    {
                        if (instance == null)
                            instance = (T)Activator.CreateInstance(typeof(T), true);
                    }
                }

                return instance;
            }
        }
    }

    public abstract class MonoSingleton_Art<T> : MonoBehaviour where T : MonoSingleton_Art<T>
    {
        private static T instance = null;

        public static bool HasInstance => instance != null;

        public static T Ins
        {
            get
            {
                if (instance == null)
                {
                    string name = typeof(T).ToString();
                    instance = new GameObject(name).AddComponent<T>();
                }

                return instance;
            }
        }

        protected virtual void Awake()
        {
            if (HasInstance)
                return;

            instance = (T)this;
            DontDestroyOnLoad(gameObject);
        }
    }
}