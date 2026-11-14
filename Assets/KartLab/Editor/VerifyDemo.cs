using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KartLab.Editor
{
    [InitializeOnLoad]
    public static class VerifyDemo
    {
        static double deadline;
        static VerifyDemo() { EditorApplication.playModeStateChanged+=State;EditorApplication.update+=Poll;deadline=EditorApplication.timeSinceStartup+300; }
        public static void Start()
        {
            EditorSceneManager.OpenScene("Assets/KartLab/Scenes/DriverLab.unity");
            SessionState.SetBool("KartLabVerify",true);EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(SessionState.GetBool("KartLabVerify",false)&&state==PlayModeStateChange.EnteredPlayMode)
                new GameObject("Verification runner").AddComponent<DemoVerification>();
        }
        static void Poll()
        {
            if(!SessionState.GetBool("KartLabVerify",false))return;
            if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("Verification timed out");SessionState.SetBool("KartLabVerify",false);EditorApplication.Exit(2);return;}
            if(!EditorApplication.isPlaying)return;
            var runner=UnityEngine.Object.FindFirstObjectByType<DemoVerification>();
            if(runner&&runner.Done){SessionState.SetBool("KartLabVerify",false);Debug.Log("KARTLAB verification failures: "+runner.Failures);EditorApplication.Exit(runner.Failures==0?0:1);}
        }
    }
}
