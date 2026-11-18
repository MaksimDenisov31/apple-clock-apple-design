using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AppleClock {
 internal static class TrafficControls {
  static readonly Style style=(Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>
<Setter Property='Width' Value='28'/><Setter Property='Height' Value='28'/><Setter Property='Padding' Value='0'/><Setter Property='Cursor' Value='Hand'/>
<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Grid Background='Transparent'><Ellipse Width='14' Height='14' Fill='{TemplateBinding Background}' Stroke='{TemplateBinding BorderBrush}' StrokeThickness='1'/><ContentPresenter x:Name='Symbol' HorizontalAlignment='Center' VerticalAlignment='Center' Opacity='0'/></Grid><ControlTemplate.Triggers>
<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Symbol' Property='Opacity' Value='0.8'/></Trigger>
<Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Symbol' Property='Opacity' Value='1'/></Trigger>
<Trigger Property='IsPressed' Value='True'><Setter Property='Opacity' Value='0.6'/></Trigger>
</ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
  internal static Button Create(int index,Action action) {
   string[] colors={"#FF5F57","#FEBC2E","#28C840"}, borders={"#E24942","#DEA124","#1EAB34"}, strokes={"#76231F","#775419","#146323"};
   string[] paths={"M 0,0 L 5,5 M 5,0 L 0,5","M 0,0 L 6,0","M 0,3 L 0,0 L 3,0 M 6,3 L 6,6 L 3,6"};
   string[] labels={"Закрыть","Свернуть","Развернуть"};
   var button=new Button{Style=style,Background=(Brush)new BrushConverter().ConvertFromString(colors[index]),BorderBrush=(Brush)new BrushConverter().ConvertFromString(borders[index]),ToolTip=index==2?"Развернуть / восстановить":labels[index],Content=new Path{Data=Geometry.Parse(paths[index]),Stroke=(Brush)new BrushConverter().ConvertFromString(strokes[index]),StrokeThickness=1}};
   System.Windows.Automation.AutomationProperties.SetName(button,labels[index]);button.Click+=delegate{action();};return button;
  }
 }
}
