using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zap.Constellation;
using Object=UnityEngine.Object;

namespace Zap.World.Tests
{
    public class GaussianEnvironmentTests
    {
        [Test]
        public void RejectsMalformedCloudAndSortsBothViewDirections()
        {
            Assert.Throws<InvalidDataException>(()=>GaussianEnvironment.Decode(new byte[8]));
            var points=new[]{Vector3.forward*2,Vector3.back,Vector3.forward*6};
            CollectionAssert.AreEqual(new[]{2,0,1},GaussianEnvironment.SortBackToFront(points,Vector3.forward));
            CollectionAssert.AreEqual(new[]{1,0,2},GaussianEnvironment.SortBackToFront(points,Vector3.back));
            using var data=new MemoryStream();using var writer=new BinaryWriter(data);
            writer.Write(0x3150535a);writer.Write(1);
            for(int i=0;i<16;i++) writer.Write(i==3?1f:0f);
            Assert.AreEqual(16,GaussianEnvironment.Decode(data.ToArray()).Length);
            writer.BaseStream.Position=8;writer.Write(float.NaN);
            Assert.Throws<InvalidDataException>(()=>GaussianEnvironment.Decode(data.ToArray()));
        }

        [UnityTest]
        public IEnumerator ThrowShattersAtDistanceWithoutAnyCollision()
        {
            var root=new GameObject("Distance journey test");var journey=root.AddComponent<WorldJourney>();
            var origin=new Vector3(1,1,1);
            try
            {
                journey.Begin(origin,Vector3.forward,null,WorldJourney.ThrowShatterDistance,4);
                Assert.IsFalse(journey.HasShattered);
                yield return new WaitForSeconds(.2f);
                Assert.IsFalse(journey.HasShattered);
                Assert.IsTrue(root.transform.Find("Travelling crystal").gameObject.activeSelf);
                yield return new WaitForSeconds(.4f);
                Assert.IsTrue(journey.HasShattered);
                Assert.Less(Vector3.Distance(root.transform.Find("Crystal fracture").position,origin+Vector3.forward*2),.001f);
                Assert.IsFalse(root.transform.Find("Travelling crystal").gameObject.activeSelf);
                Assert.IsTrue(journey.IsActive,"A completed flight must still wait for world readiness.");
                journey.Cancel();Assert.IsFalse(journey.IsActive);
            }
            finally {Object.Destroy(root);}
        }
    }
}
