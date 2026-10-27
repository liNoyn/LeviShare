using LeviShare.App;

namespace LeviShare.Tests;

public sealed class ShareSectionTests
{
    [Fact]
    public void FilteredBulkSelectionPreservesHiddenItems()
    {
        var changes = 0;
        var section = new ShareSection("Mods", "", () => changes++);
        var first = new ShareItem(ItemKind.RawMod, "one", "First", "v1.0", false, () => changes++);
        var second = new ShareItem(ItemKind.RawMod, "two", "Second", "v2.0", true, () => changes++);
        section.Items.Add(first);
        section.Items.Add(second);
        section.Refresh("FIRST");

        section.SetAll(true);
        Assert.True(first.IsChecked);
        Assert.True(second.IsChecked);
        Assert.Equal(1, changes);
        Assert.Equal("2 / 2", section.CountLabel);

        section.SetAll(false);
        Assert.False(first.IsChecked);
        Assert.True(second.IsChecked);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void SearchMatchesDetailsAndClearingRestoresItems()
    {
        var section = new ShareSection("Packs", "", () => { });
        section.Items.Add(new ShareItem(ItemKind.Pack, "a", "日本語パック", "1.2 · MyFolder", false, () => { }));
        section.Refresh("myfolder");
        Assert.True(section.HasMatches);
        Assert.Single(section.VisibleItems);
        section.Refresh("missing");
        Assert.False(section.HasMatches);
        section.Refresh("");
        Assert.Single(section.VisibleItems);
    }

    [Fact]
    public void UpdatingCountsDoesNotRebuildFilteredRows()
    {
        var section = new ShareSection("Mods", "", () => { });
        var notifications = new List<string?>();
        section.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        section.Refresh("test");
        notifications.Clear();
        section.Refresh("test");
        Assert.Contains(nameof(ShareSection.CountLabel), notifications);
        Assert.DoesNotContain(nameof(ShareSection.VisibleItems), notifications);
    }

    [Fact]
    public void SelectedOnlyCombinesWithSearchAndUpdatesAfterUnticking()
    {
        var section = new ShareSection("Mods", "", () => { });
        var selected = new ShareItem(ItemKind.RawMod, "a", "Alpha", "1.0", true, () => { });
        section.Items.Add(selected);
        section.Items.Add(new ShareItem(ItemKind.RawMod, "b", "Alpha disabled", "1.0", false, () => { }));
        section.Items.Add(new ShareItem(ItemKind.RawMod, "c", "Beta", "2.0", true, () => { }));
        section.Refresh("alpha", selectedOnly: true);
        Assert.Same(selected, Assert.Single(section.VisibleItems));
        section.SetAll(false);
        section.Refresh("alpha", selectedOnly: true);
        Assert.False(section.HasMatches);
        Assert.True(section.Items[2].IsChecked);
        section.Refresh("");
        Assert.Equal(3, section.VisibleItems.Count());
    }
}
