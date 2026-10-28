using S100FC;
using System;
using Xunit;
using ElectronicProduct = S100FC.S128.FeatureTypes.ElectronicProduct;
using CatalogueSectionHeader = S100FC.S128.InformationTypes.CatalogueSectionHeader;
using ProductMapping = S100FC.S128.FeatureAssociation.ProductMapping;
using InclusionType = S100FC.S122.InformationAssociation.InclusionType;

namespace TestCatalogueBuilder;

public class FlattenTests
{
    [Fact]
    public void FeatureType_FlattenAndUnflatten_PreservesObject() {
        FeatureType original = new ElectronicProduct {
            notForNavigation = true,
            datasetName = "101DK0040545E",
            issueDate = new DateOnly(2026, 10, 28),
            issueTime = new S100FC.S100.Time(9, 30),
            catalogueElementClassification = [1, 2],
            productSpecification = new S100FC.S128.ComplexAttributes.productSpecification {
                name = "S-101",
                version = "2.0.0",
                editionDate = new DateOnly(2024, 10, 16),
            },
        };

        var json = original.Flatten();
        var restored = AttributeFlattenExtensions.Unflatten<FeatureType>(json, original.GetType());

        Assert.NotSame(original, restored);
        Assert.IsType<ElectronicProduct>(restored);
        Assert.Equivalent(original, restored, strict: true);
    }

    [Fact]
    public void InformationType_FlattenAndUnflatten_PreservesObject() {
        InformationType original = new CatalogueSectionHeader {
            catalogueSectionNumber = 12,
            catalogueSectionTitle = "Danish nautical products",
            information = new S100FC.S128.ComplexAttributes.information {
                headline = "Catalogue notes",
                language = "eng",
                text = ["First note", "Second note"],
            },
        };

        var json = original.Flatten();
        var restored = AttributeFlattenExtensions.Unflatten<InformationType>(json, original.GetType());

        Assert.NotSame(original, restored);
        Assert.IsType<CatalogueSectionHeader>(restored);
        Assert.Equivalent(original, restored, strict: true);
    }

    [Fact]
    public void FeatureBinding_FlattenAndUnflatten_PreservesObject() {
        featureBinding original = new featureBinding<ProductMapping> {
            roleType = "association",
            role = "theSource",
            featureType = "ElectronicProduct",
            featureId = "feature-123",
            Primitive = Primitives.surface,
            association = new ProductMapping {
                categoryOfProductMapping = 2,
            },
        };

        var json = original.Flatten();
        var restored = AttributeFlattenExtensions.Unflatten<featureBinding>(json, original.GetType());

        Assert.NotSame(original, restored);
        Assert.IsType<featureBinding<ProductMapping>>(restored);
        Assert.IsType<ProductMapping>(restored.association);
        Assert.Equivalent(original, restored, strict: true);
    }

    [Fact]
    public void InformationBinding_FlattenAndUnflatten_PreservesObject() {
        informationBinding original = new informationBinding<InclusionType> {
            roleType = "association",
            role = "theApplicableRxN",
            informationType = "Applicability",
            informationId = "information-456",
            association = new InclusionType {
                membership = 1,
            },
        };

        var json = original.Flatten();
        var restored = AttributeFlattenExtensions.Unflatten<informationBinding>(json, original.GetType());

        Assert.NotSame(original, restored);
        Assert.IsType<informationBinding<InclusionType>>(restored);
        Assert.IsType<InclusionType>(restored.association);
        Assert.Equivalent(original, restored, strict: true);
    }
}
