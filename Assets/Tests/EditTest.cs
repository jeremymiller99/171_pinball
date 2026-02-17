using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.PerformanceTesting;
using UnityEngine.SceneManagement;

public class EditTest
{
    // Tests are based on examples from: 
    // https://docs.unity3d.com/Packages/com.unity.test-framework.performance@3.2/manual/writing-tests.html

    [Test]
    public void EditTestSimplePasses()
    {
        Measure_Empty();
    }

    // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
    // `yield return null;` to skip a frame.
    [UnityTest]
    public IEnumerator EditTestWithEnumeratorPasses()
    {
        MainSceneFrameTime_StartPosition();
        GameSceneFrameTime_StartPosition();
        BoardSceneFrameTime_StartPosition();
        yield return null;
    }



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
