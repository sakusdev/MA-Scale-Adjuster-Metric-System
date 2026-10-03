using NUnit.Framework;
using UnityEngine;

namespace SakusDev.MAScaleAdjusterMetricSystem.Tests
{
    public class MetricMathTests
    {
        [Test]
        public void UnityUnits_AreMetres()
        {
            var parent = new GameObject("parent");
            var child = new GameObject("child");
            try
            {
                child.transform.SetParent(parent.transform, false);
                child.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                Assert.That(Vector3.Distance(parent.transform.position, child.transform.position), Is.EqualTo(0.5f).Within(1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(child);
                Object.DestroyImmediate(parent);
            }
        }
    }
}
