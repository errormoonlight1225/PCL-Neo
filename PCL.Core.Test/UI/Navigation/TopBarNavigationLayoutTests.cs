using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.UI.Navigation;

namespace PCL.Core.Test.UI.Navigation;

[TestClass]
public sealed class TopBarNavigationLayoutTests
{
    // 启动、下载、扩展（基岩版 + Lunar）、工具、设置、插件
    private static readonly TopBarNavigationItem[] SixTabs =
    [
        new(0, 80), new(1, 90), new(2, 100),
        new(4, 90), new(3, 90), new(14, 90)
    ];

    [TestMethod]
    public void AllTabsRemainVisibleWhenMeasuredWidthFits()
    {
        var collapsed = TopBarNavigationLayout.SelectCollapsedTabs(SixTabs,
            new HashSet<int> { 3, 14 }, availableWidth: 560, overflowButtonWidth: 32);

        Assert.AreEqual(0, collapsed.Count);
    }

    [TestMethod]
    public void PreferredTabsCollapseOnlyWhenTheCurrentWidthRequiresIt()
    {
        var collapsed = TopBarNavigationLayout.SelectCollapsedTabs(SixTabs,
            new HashSet<int> { 3, 14 }, availableWidth: 460, overflowButtonWidth: 32);

        Assert.IsTrue(collapsed.Count > 0);
        CollectionAssert.AreEquivalent(new[] { 3, 14 }, collapsed.ToArray());
        Assert.IsFalse(collapsed.Contains(TopBarNavigationLayout.LaunchTabId));
        var visibleWidth = SixTabs.Where(tab => !collapsed.Contains(tab.Id)).Sum(tab => tab.Width) + 32;
        Assert.IsTrue(visibleWidth <= 460);
    }

    [TestMethod]
    public void FiveOrFewerTabsAreNeverAutomaticallyCollapsed()
    {
        var collapsed = TopBarNavigationLayout.SelectCollapsedTabs(SixTabs.Take(5).ToArray(),
            new HashSet<int> { 1, 2, 3 }, availableWidth: 1, overflowButtonWidth: 32);

        Assert.AreEqual(0, collapsed.Count);
    }

    [TestMethod]
    public void NarrowWidthCollapsesOptionalTabsButKeepsLaunchAndExtensions()
    {
        var collapsed = TopBarNavigationLayout.SelectCollapsedTabs(SixTabs,
            new HashSet<int>(), availableWidth: 120, overflowButtonWidth: 32);

        CollectionAssert.AreEquivalent(new[] { 1, 4, 3, 14 }, collapsed.ToArray());
    }

    [TestMethod]
    public void ExtensionsTabIgnoresCollapsePreference()
    {
        var collapsed = TopBarNavigationLayout.SelectCollapsedTabs(SixTabs,
            new HashSet<int> { TopBarNavigationLayout.ExtensionsTabId }, availableWidth: 460, overflowButtonWidth: 32);

        Assert.IsTrue(collapsed.Count > 0);
        Assert.IsFalse(collapsed.Contains(TopBarNavigationLayout.ExtensionsTabId));
        Assert.IsFalse(collapsed.Contains(TopBarNavigationLayout.LaunchTabId));
    }

}
