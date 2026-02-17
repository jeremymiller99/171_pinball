using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.PerformanceTesting;
using UnityEngine.SceneManagement;

public class PlayTest
{

    // Tests are based on examples from: 
    // https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.2/manual/writing-tests.html

    [Test, Performance, Version("1")]
    public void Measure_Empty()
    {
        var allocated = new SampleGroup("TotalAllocatedMemory", SampleUnit.Megabyte);
        var reserved = new SampleGroup("TotalReservedMemory", SampleUnit.Megabyte);
        Measure.Custom(allocated, UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f);
        Measure.Custom(reserved, UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1048576f);
    }

    [UnityTest, Performance, Version("4")]
    public IEnumerator MainSceneFrameTime_StartPosition()
    {
        SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);

        // Measure initial time of first 25 frames after loading the scene
        using(Measure.Frames().Scope("FrameTime.FirstFramesAfterLoadingScene"))
        {
            for (var i = 0; i < 25; i++)
            {
                yield return null;
            }
        }

        // Measure frame times for ten seconds during rest of the "Demo" scene
        using (Measure.Frames().Scope("FrameTime.Main"))
        {
            yield return new WaitForSeconds(10);
        }
    }

    [UnityTest, Performance, Version("4")]
    public IEnumerator GameSceneFrameTime_StartPosition()
    {
        SceneManager.LoadScene("GameplayCore", LoadSceneMode.Single);

        // Measure initial time of first 25 frames after loading the scene
        using(Measure.Frames().Scope("FrameTime.FirstFramesAfterLoadingScene"))
        {
            for (var i = 0; i < 25; i++)
            {
                yield return null;
            }
        }

        // Measure frame times for ten seconds during rest of the "Demo" scene
        using (Measure.Frames().Scope("FrameTime.Main"))
        {
            yield return new WaitForSeconds(10);
        }
    }

    [UnityTest, Performance, Version("4")]
    public IEnumerator BoardSceneFrameTime_StartPosition()
    {
        SceneManager.LoadScene("Board_Alpha", LoadSceneMode.Single);

        // Measure initial time of first 25 frames after loading the scene
        using(Measure.Frames().Scope("FrameTime.FirstFramesAfterLoadingScene"))
        {
            for (var i = 0; i < 25; i++)
            {
                yield return null;
            }
        }

        // Measure frame times for ten seconds during rest of the "Demo" scene
        using (Measure.Frames().Scope("FrameTime.Main"))
        {
            yield return new WaitForSeconds(10);
        }
    }

}
