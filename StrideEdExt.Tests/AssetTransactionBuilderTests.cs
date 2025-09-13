using Stride.Core;
using StrideEdExt.StrideAssetExt.Assets.Transaction;

namespace StrideEdExt.Tests;

public class AssetTransactionBuilderTests
{
    [Fact]
    public void AssetPropertyAndFieldChangesTest()
    {
        var testAsset = new TestObjectAsset
        {
            StringField1 = "TestString1",
            StringField2 = "TestString2",
            StringProp1 = "TestString3",
            StringProp2 = "TestString4",

            InnerAsset = new TestInnerAsset
            {
                IntField1 = 12,
                IntProp1 = 22,
            },
            InnerAssetStruct = new TestInnerAssetStruct
            {
                IntField1 = 33
            },
        };
        var builder = AssetTransactionBuilder.Begin(testAsset);
        testAsset.StringField1 = null;
        testAsset.StringField2 = "NewString2";
        testAsset.StringProp1  = null;
        testAsset.StringProp2 = "NewString4";

        testAsset.InnerAsset.IntField1 *= 10;
        testAsset.InnerAsset.IntProp1 *= 10;

        testAsset.InnerAssetStruct = new TestInnerAssetStruct
        {
            IntField1 = 55,
        };

        var changes = builder.GetAllChanges();
        Assert.True(changes.Count == 7);
    }

    [Fact]
    public void ObjectPropertyChangeTest()
    {
        var testAsset = new TestObjectAsset
        {
            InnerAsset = new TestInnerAsset
            {
                IntField1 = 12,
                IntProp1 = 22,
            },
        };
        var builder = AssetTransactionBuilder.Begin(testAsset);

        testAsset.InnerAsset = new TestInnerAsset
        {
            IntField1 = 120,
            IntProp1 = 220,
        };

        var changes = builder.GetAllChanges();
        Assert.True(changes.Count == 1);
    }

    [Fact]
    public void ListItemValueChangeTest()
    {
        var testListAsset = new TestListAsset();
        testListAsset.PopulateList(2);

        var builder = AssetTransactionBuilder.Begin(testListAsset);

        foreach (var item in testListAsset.AssetList)
        {
            item.IntField1 *= 10;
            item.IntProp1 *= 10;
        }

        var changes = builder.GetAllChanges();
        Assert.True(changes.Count == 4);
    }

    [Fact]
    public void ListInsertItemMiddleTest()
    {
        var testListAsset = new TestListAsset();
        testListAsset.PopulateList(2);

        var builder = AssetTransactionBuilder.Begin(testListAsset);

        testListAsset.AssetList.Insert(index: 1, new TestInnerAsset());

        var changes = builder.GetAllChanges();
        Assert.True(changes.Count == 1);
    }

    [Fact]
    public void ListInsertItemTest()
    {
        var testListAsset = new TestListAsset();
        testListAsset.PopulateList(3);

        var builder = AssetTransactionBuilder.Begin(testListAsset);

        testListAsset.AssetList.Insert(index: 1, new TestInnerAsset());

        var changes = builder.GetAllChanges();
        Assert.True(changes.Count == 1);
    }

    [Fact]
    public void ListItemRemovedFirstTest()
    {
        var testListAsset = new TestListAsset();
        testListAsset.PopulateList(3);

        var builder = AssetTransactionBuilder.Begin(testListAsset);

        testListAsset.AssetList.RemoveAt(0);

        var changes = builder.GetAllChanges();
        Assert.True(changes.Count == 1);
    }

    [Fact]
    public void ListItemRemovedLastTest()
    {
        var testListAsset = new TestListAsset();
        testListAsset.PopulateList(3);

        var builder = AssetTransactionBuilder.Begin(testListAsset);

        testListAsset.AssetList.RemoveAt(1);

        var changes = builder.GetAllChanges();
        Assert.True(changes.Count == 1);
    }

    [Fact]
    public void ListItemRemovedMiddleTest()
    {
        var testListAsset = new TestListAsset();
        testListAsset.PopulateList(5);

        var builder = AssetTransactionBuilder.Begin(testListAsset);

        testListAsset.AssetList.RemoveAt(1);

        var changes = builder.GetAllChanges();
        Assert.True(changes.Count == 1);
    }

    [Fact]
    public void ListItemRemovedTwiceSameIndexTest()
    {
        var testListAsset = new TestListAsset();
        testListAsset.PopulateList(5);

        var builder = AssetTransactionBuilder.Begin(testListAsset);

        testListAsset.AssetList.RemoveAt(1);
        testListAsset.AssetList.RemoveAt(1);

        var changes = builder.GetAllChanges();
        Assert.True(changes.Count == 2);
    }

    [Fact]
    public void ListItemRemovedTwiceTest()
    {
        var testListAsset = new TestListAsset();
        testListAsset.PopulateList(5);

        var builder = AssetTransactionBuilder.Begin(testListAsset);

        testListAsset.AssetList.RemoveAt(1);
        testListAsset.AssetList.RemoveAt(2);

        var changes = builder.GetAllChanges();
        Assert.True(changes.Count == 2);
    }

    [Fact]
    public void DictionaryInsertItemTest()
    {
        var testDictAsset = new TestDictionaryAsset();
        testDictAsset.PopulateDictionary(3);

        var builder = AssetTransactionBuilder.Begin(testDictAsset);

        testDictAsset.AssetDict[10] = new TestInnerAsset();

        var changes = builder.GetAllChanges();
        Assert.True(changes.Count == 1);
    }

    [Fact]
    public void DictionaryItemRemovedTest()
    {
        var testDictAsset = new TestDictionaryAsset();
        testDictAsset.PopulateDictionary(3);

        var builder = AssetTransactionBuilder.Begin(testDictAsset);

        testDictAsset.AssetDict.Remove(1);

        var changes = builder.GetAllChanges();
        Assert.True(changes.Count == 1);
    }

    [Fact]
    public void DictionaryInsertAndRemoveItemTest()
    {
        var testDictAsset = new TestDictionaryAsset();
        testDictAsset.PopulateDictionary(3);

        var builder = AssetTransactionBuilder.Begin(testDictAsset);

        testDictAsset.AssetDict.Remove(1);
        testDictAsset.AssetDict[10] = new TestInnerAsset();

        var changes = builder.GetAllChanges();
        Assert.True(changes.Count == 2);
    }

    [DataContract]
    public class TestObjectAsset
    {
        public string? StringField1;
        public string? StringField2;
        public string? StringProp1 { get; set; }
        public string? StringProp2 { get; set; }

        public TestInnerAsset? InnerAsset { get; set; }

        public TestInnerAssetStruct InnerAssetStruct;

        public List<TestInnerAsset>? AssetList;
    }

    [DataContract]
    public class TestListAsset
    {
        public List<TestInnerAsset> AssetList = [];

        public void PopulateList(int count)
        {
            for (int i = 0; i < count; i++)
            {
                AssetList.Add(new TestInnerAsset
                {
                    IntField1 = i + 1,
                    IntProp1 = i + 2,
                });
            }
        }
    }

    [DataContract]
    public class TestDictionaryAsset
    {
        public Dictionary<int, TestInnerAsset> AssetDict = [];

        public void PopulateDictionary(int count)
        {
            for (int i = 0; i < count; i++)
            {
                AssetDict[i] = new TestInnerAsset
                {
                    IntField1 = i + 1,
                    IntProp1 = i + 2,
                };
            }
        }
    }

    [DataContract]
    public class TestInnerAsset
    {
        public int IntField1;
        public int? IntProp1 { get; set; }
    }

    [DataContract]
    public struct TestInnerAssetStruct
    {
        public int IntField1;
    }
}