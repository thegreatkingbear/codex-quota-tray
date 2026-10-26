using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace CodexQuotaTray.App;

internal static class ActionButtons
{
    // A compact IDE-style action: quiet at rest, visible hover/press and keyboard focus.
    private static readonly Style Compact = (Style)XamlReader.Parse("""
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
          <Setter Property="Background" Value="Transparent"/>
          <Setter Property="Foreground" Value="{DynamicResource Quota.Text}"/>
          <Setter Property="BorderBrush" Value="Transparent"/>
          <Setter Property="BorderThickness" Value="1"/>
          <Setter Property="Padding" Value="9,4"/>
          <Setter Property="FontSize" Value="12"/>
          <Setter Property="FontFamily" Value="Segoe UI"/>
          <Setter Property="MinHeight" Value="28"/>
          <Setter Property="MinWidth" Value="28"/>
          <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
          <Setter Property="Template">
            <Setter.Value>
              <ControlTemplate TargetType="Button">
                <Border x:Name="Surface" Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="4" Padding="{TemplateBinding Padding}">
                  <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="True"/>
                </Border>
                <ControlTemplate.Triggers>
                  <Trigger Property="IsMouseOver" Value="True">
                    <Setter TargetName="Surface" Property="Background" Value="{DynamicResource Quota.Track}"/>
                  </Trigger>
                  <Trigger Property="IsPressed" Value="True">
                    <Setter TargetName="Surface" Property="Background" Value="{DynamicResource Quota.Border}"/>
                  </Trigger>
                  <Trigger Property="IsKeyboardFocused" Value="True">
                    <Setter TargetName="Surface" Property="BorderBrush" Value="{DynamicResource {x:Static SystemColors.HighlightBrushKey}}"/>
                  </Trigger>
                  <Trigger Property="IsEnabled" Value="False">
                    <Setter TargetName="Surface" Property="Opacity" Value="0.4"/>
                  </Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate>
            </Setter.Value>
          </Setter>
        </Style>
        """);

    internal static Button Create(string label, string? icon = null, bool showLabel = true)
    {
        var button = new Button { Style = Compact, ToolTip = label };
        AutomationProperties.SetName(button, label);
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (icon is not null)
            content.Children.Add(new TextBlock { Text = icon, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, showLabel ? 6 : 0, 0) });
        if (showLabel) content.Children.Add(new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        button.Content = content;
        return button;
    }
}
