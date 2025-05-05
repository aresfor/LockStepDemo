using System.IO;
using UnityEngine;


    public static partial class PathUtil {
        public static string GetUnityPath(string path){
            return Path.Combine(Application.dataPath, path);
        }
    }
