using System.Collections.Generic;
using UnityEngine;

public class MovingAverageFilter
{
    private Queue<Vector3> samples = new();
    private int sampleCount;
    private Vector3 average;

    public MovingAverageFilter(int sampleCount)
    {
        this.sampleCount = sampleCount;
    }

    public Vector3 Filter(Vector3 newSample)
    {
        samples.Enqueue(newSample);
        if (samples.Count > sampleCount)
        {
            samples.Dequeue();
        }

        average = Vector3.zero;
        foreach (var sample in samples)
        {
            average += sample;
        }
        average /= samples.Count;

        return average;
    }
}
