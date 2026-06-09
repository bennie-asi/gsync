using System.Text;
using System.Xml;
using GSYNC.App.Infrastructure.Localization;
using GSYNC.App.Primitives;
using GSYNC.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace GSYNC.App.Infrastructure.Localization;

public static class LocalizedTableTemplates
{
    public static DataTemplate CreateTextTemplate(string bindingPath, bool isBold = false)
    {
        var fontWeight = isBold ? "SemiBold" : "Normal";
        var foreground = isBold ? "{ThemeResource AppTextPrimaryBrush}" : "{ThemeResource AppTextSecondaryBrush}";
        var xaml = $$"""
<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"
              xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"
              xmlns:primitives=\"using:GSYNC.App.Primitives\">
    <primitives:TooltipTextBlock Text=\"{Binding {{bindingPath}}}\" FontWeight=\"{{fontWeight}}\" Foreground=\"{{foreground}}\" />
</DataTemplate>
""";
        return (DataTemplate)XamlReader.Load(xaml);
    }

    public static DataTemplate CreateBadgeTemplate(string bindingPath)
    {
        var xaml = $$"""
<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"
              xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"
              xmlns:primitives=\"using:GSYNC.App.Primitives\">
    <primitives:Badge Text=\"{Binding {{bindingPath}}}\" Variant=\"{Binding {{bindingPath}}}\" />
</DataTemplate>
""";
        return (DataTemplate)XamlReader.Load(xaml);
    }

    public static DataTemplate CreateCheckBoxTemplate(string bindingPath)
    {
        var xaml = $$"""
<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"
              xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">
    <CheckBox IsChecked=\"{Binding {{bindingPath}}, Mode=TwoWay}\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Center\" />
</DataTemplate>
""";
        return (DataTemplate)XamlReader.Load(xaml);
    }

    public static DataTemplate CreateLibraryActionsTemplate()
    {
        var xaml = """
<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid ColumnSpacing="6">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="Auto" />
        </Grid.ColumnDefinitions>
        <Button Content="Open" Click="OpenGameDetails_Click" Style="{StaticResource SecondaryToolbarButtonStyle}" />
        <Button Grid.Column="1" Content="More" Style="{StaticResource SecondaryToolbarButtonStyle}" />
    </Grid>
</DataTemplate>
""";
        return (DataTemplate)XamlReader.Load(xaml);
    }
}
