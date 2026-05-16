using S100FC;
using System;
using System.Collections.Generic;
using System.Text;

namespace TestCatalogueBuilder
{
    public class TestFlattenExtension
    {
        [Fact]
        public void TestUnflatten() {
            var v = AttributeFlattenExtensions.Unflatten<FeatureType>(json, typeof(S100FC.S128.FeatureTypes.ElectronicProduct));

            System.Diagnostics.Debugger.Break();
        }

        const string json = "{\n  \"notForNavigation\": true,\n  \"issueDate\": \"2026-02-12\",\n  \"typeOfProductFormat\": 2,\n  \"catalogueElementClassification[0]\": 1,\n  \"editionNumber\": 12,\n  \"updateNumber\": 0,\n  \"datasetName\": \"101DK0040545E\",\n  \"specificUsage\": 4,\n  \"productSpecification.editionDate\": \"2024-10-16\",\n  \"productSpecification.name\": \"S-101\",\n  \"productSpecification.version\": \"2.0.0\"\n}";
    }    
}
