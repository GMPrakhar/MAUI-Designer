import { TestBed } from '@angular/core/testing';
import { XamlParserService } from './xaml-parser';
import { XamlGeneratorService } from './xaml-generator';
import { ElementType, MauiElement } from '../models/maui-element';

const page = (body: string) => `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="YourApp.MainPage">
    <VerticalStackLayout x:Name="Root">
${body}
    </VerticalStackLayout>
</ContentPage>`;

describe('XAML round trip fidelity', () => {
  let parser: XamlParserService;
  let generator: XamlGeneratorService;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    parser = TestBed.inject(XamlParserService);
    generator = TestBed.inject(XamlGeneratorService);
  });

  const firstChild = (xaml: string): MauiElement => parser.parseXaml(xaml).children[0];

  describe('unmodelled attributes', () => {
    it('keeps attributes the designer does not model on a built in control', () => {
      const label = firstChild(page('<Label x:Name="L" Text="Hi" Rotation="15" Opacity="0.5" />'));

      expect(label.type).toBe(ElementType.Label);
      expect(label.properties.rawAttributes).toEqual({ Rotation: '15', Opacity: '0.5' });
    });

    it('re emits preserved attributes so an import export cycle is not lossy', () => {
      const root = parser.parseXaml(page('<Label x:Name="L" Text="Hi" Rotation="15" Opacity="0.5" />'));

      const xaml = generator.generateXaml(root);

      expect(xaml).toContain('Rotation="15"');
      expect(xaml).toContain('Opacity="0.5"');
    });

    it('does not duplicate attributes the designer already models', () => {
      const root = parser.parseXaml(page('<Label x:Name="L" Text="Hi" FontSize="22" />'));

      const xaml = generator.generateXaml(root);

      expect(xaml.match(/FontSize=/g)?.length).toBe(1);
      expect(xaml).toContain('FontSize="22"');
    });

    it('leaves bound attributes to the binding pass rather than preserving them twice', () => {
      const label = firstChild(page('<Label x:Name="L" Text="{Binding UserName}" />'));

      expect(label.properties.bindings).toEqual({ Text: 'UserName' });
      expect(label.properties.rawAttributes).toBeUndefined();
    });

    it('never preserves namespace declarations as element attributes', () => {
      const root = parser.parseXaml(`<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    <VerticalStackLayout x:Name="Root" xmlns:local="clr-namespace:App" Rotation="4">
        <Label x:Name="L" />
    </VerticalStackLayout>
</ContentPage>`);

      expect(root.properties.rawAttributes).toEqual({ Rotation: '4' });
    });

    it('does not preserve a themed colour as a raw attribute as well', () => {
      const markup = '<Label x:Name="L" Text="Hi" TextColor="{AppThemeBinding Light=#111111, Dark=#eeeeee}" />';
      const label = firstChild(page(markup));

      expect(label.properties.appTheme?.['TextColor']).toEqual({ light: '#111111', dark: '#eeeeee' });
      expect(label.properties.rawAttributes?.['TextColor']).toBeUndefined();

      const xaml = generator.generateXaml(parser.parseXaml(page(markup)));
      expect(xaml.match(/TextColor=/g)?.length).toBe(1);
    });

    it('escapes preserved values so injected markup cannot break the document', () => {      const root = parser.parseXaml(page('<Label x:Name="L" Text="Hi" AutomationId="a&quot;b&amp;c" />'));

      const xaml = generator.generateXaml(root);

      expect(xaml).toContain('AutomationId="a&quot;b&amp;c"');
    });
  });

  describe('LayoutOptions', () => {
    it('parses HorizontalOptions and VerticalOptions', () => {
      const label = firstChild(page('<Label x:Name="L" HorizontalOptions="Center" VerticalOptions="Fill" />'));

      expect(label.properties.horizontalOptions).toBe('Center');
      expect(label.properties.verticalOptions).toBe('Fill');
    });

    it('normalises the legacy AndExpand suffix inherited from Xamarin.Forms', () => {
      expect(XamlParserService.parseLayoutOptions('EndAndExpand')).toBe('End');
      expect(XamlParserService.parseLayoutOptions('StartAndExpand')).toBe('Start');
    });

    it('accepts the fully qualified LayoutOptions.Center form', () => {
      expect(XamlParserService.parseLayoutOptions('LayoutOptions.Center')).toBe('Center');
    });

    it('rejects values that are not LayoutOptions', () => {
      expect(XamlParserService.parseLayoutOptions('Middle')).toBeNull();
    });

    it('preserves an unrecognised alignment verbatim instead of dropping it', () => {
      const label = firstChild(page('<Label x:Name="L" HorizontalOptions="{StaticResource Wide}" />'));

      expect(label.properties.horizontalOptions).toBeUndefined();
      expect(label.properties.rawAttributes?.['HorizontalOptions']).toBe('{StaticResource Wide}');
    });

    it('generates the alignment attributes', () => {
      const root = parser.parseXaml(page('<Label x:Name="L" HorizontalOptions="Center" VerticalOptions="End" />'));

      const xaml = generator.generateXaml(root);

      expect(xaml).toContain('HorizontalOptions="Center"');
      expect(xaml).toContain('VerticalOptions="End"');
    });

    it('omits the alignment attributes when the element inherits them', () => {
      const root = parser.parseXaml(page('<Label x:Name="L" Text="Hi" />'));

      const xaml = generator.generateXaml(root);

      expect(xaml).not.toContain('HorizontalOptions=');
      expect(xaml).not.toContain('VerticalOptions=');
    });

    it('survives a full parse generate parse cycle', () => {
      const original = page('<Label x:Name="L" Text="Hi" HorizontalOptions="Center" VerticalOptions="Fill" />');

      const reparsed = firstChild(generator.generateXaml(parser.parseXaml(original)));

      expect(reparsed.properties.horizontalOptions).toBe('Center');
      expect(reparsed.properties.verticalOptions).toBe('Fill');
    });

    describe('real-world custom pages', () => {
      // Adapted from dotnet/maui EntryPage.xaml at d2edf1972d09f6689a4621ba9ff42346ced6f1b1.
      // The source is MIT licensed by the .NET Foundation and Contributors.
      const entryPageExcerpt = `<views:BasePage
      xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
      xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
      xmlns:controls="clr-namespace:Maui.Controls.Sample.Pages"
      xmlns:views="clr-namespace:Maui.Controls.Sample.Pages.Base"
      xmlns:viewmodels="clr-namespace:Maui.Controls.Sample.ViewModels"
      x:Class="Maui.Controls.Sample.Pages.EntryPage"
      Title="Entry">
    <views:BasePage.Resources>
      <ResourceDictionary>
        <Style x:Key="EntryVisualStatesStyle" TargetType="Entry">
          <Setter Property="VisualStateManager.VisualStateGroups">
            <VisualStateGroupList>
              <VisualStateGroup x:Name="CommonStates">
                <VisualState x:Name="Focused">
                  <VisualState.Setters>
                    <Setter Property="BackgroundColor" Value="Yellow" />
                  </VisualState.Setters>
                </VisualState>
              </VisualStateGroup>
            </VisualStateGroupList>
          </Setter>
        </Style>
      </ResourceDictionary>
    </views:BasePage.Resources>
    <views:BasePage.BindingContext>
      <viewmodels:EntryViewModel />
    </views:BasePage.BindingContext>
    <views:BasePage.Content>
      <ScrollView>
        <VerticalStackLayout Padding="12">
          <Label Text="Password" Style="{StaticResource Headline}" />
          <HorizontalStackLayout>
            <CheckBox x:Name="chkIsPassword" IsChecked="true" />
            <Label Text="Is Password" VerticalOptions="Center" />
          </HorizontalStackLayout>
          <Entry IsPassword="{Binding IsChecked, Source={Reference chkIsPassword}}" />
          <Entry Text="Background">
            <Entry.Background>
              <LinearGradientBrush EndPoint="1,0">
                <GradientStop Color="Yellow" Offset="0.1" />
                <GradientStop Color="Green" Offset="1.0" />
              </LinearGradientBrush>
            </Entry.Background>
          </Entry>
          <controls:TransparentEntry />
          <HorizontalStackLayout>
            <Label Text="CursorPosition = 4" />
            <Slider x:Name="sldCursorPosition" WidthRequest="100" />
          </HorizontalStackLayout>
        </VerticalStackLayout>
      </ScrollView>
    </views:BasePage.Content>
  </views:BasePage>`;

      it('finds the visual content inside a custom page Content property', () => {
        const root = parser.parseXaml(entryPageExcerpt);

        expect(root.type).toBe(ElementType.ScrollView);
        expect(root.children[0].type).toBe(ElementType.VerticalStackLayout);
        expect(root.children[0].children.map(child => child.type)).toEqual([
          ElementType.Label,
          ElementType.StackLayout,
          ElementType.Entry,
          ElementType.Entry,
          ElementType.Custom,
          ElementType.StackLayout
        ]);
      });

      it('preserves page resources, binding context, namespaces, and the custom page root', () => {
        const generated = generator.generateXaml(parser.parseXaml(entryPageExcerpt));

        expect(generated).toContain('<views:BasePage');
        expect(generated).toContain('xmlns:views="clr-namespace:Maui.Controls.Sample.Pages.Base"');
        expect(generated).toContain('x:Class="Maui.Controls.Sample.Pages.EntryPage"');
        expect(generated).toContain('Title="Entry"');
        expect(generated).toContain('<views:BasePage.Resources>');
        expect(generated).toContain('<VisualState x:Name="Focused">');
        expect(generated).toContain('<views:BasePage.BindingContext>');
        expect(generated).toContain('<viewmodels:EntryViewModel');
        expect(generated).toContain('<views:BasePage.Content>');
        expect(generated).toContain('</views:BasePage>');
      });

      it('round trips nested reference bindings and built-in property elements', () => {
        const root = parser.parseXaml(entryPageExcerpt);
        const generated = generator.generateXaml(root);
        const gradientEntry = root.children[0].children[3];

        expect(generated).toContain('IsPassword="{Binding IsChecked, Source={Reference chkIsPassword}}"');
        expect(generated).toContain('<Entry.Background>');
        expect(generated).toContain('<LinearGradientBrush EndPoint="1,0">');
        expect(generated).toContain('<GradientStop Color="Green" Offset="1.0"');
        expect(gradientEntry.properties.backgroundGradient)
          .toBe('linear-gradient(90deg, Yellow 10%, Green 100%)');
      });

      it('renders a gradient from its StartPoint to its EndPoint', () => {
        const xaml = entryPageExcerpt
          .replace('EndPoint="1,0"', 'StartPoint="1,0" EndPoint="0,0"');
        const gradientEntry = parser.parseXaml(xaml).children[0].children[3];

        expect(gradientEntry.properties.backgroundGradient)
          .toBe('linear-gradient(270deg, Yellow 10%, Green 100%)');
      });

      it('retains namespaces used only by visual-tree attributes and property elements', () => {
        const xaml = entryPageExcerpt
          .replace(
            'xmlns:controls="clr-namespace:Maui.Controls.Sample.Pages"',
            'xmlns:controls="clr-namespace:Maui.Controls.Sample.Pages"\n      xmlns:toolkit="http://schemas.microsoft.com/dotnet/2022/maui/toolkit"'
          )
          .replace(
            '<Label Text="Password" Style="{StaticResource Headline}" />',
            `<Label Text="Password" toolkit:SemanticOrderView.Order="1">
              <Label.Behaviors>
                <toolkit:TouchBehavior />
              </Label.Behaviors>
            </Label>`
          );

        const generated = generator.generateXaml(parser.parseXaml(xaml));

        expect(generated).toContain(
          'xmlns:toolkit="http://schemas.microsoft.com/dotnet/2022/maui/toolkit"'
        );
        expect(generated).toContain('toolkit:SemanticOrderView.Order="1"');
        expect(generated).toContain('<toolkit:TouchBehavior');
      });

      it('preserves namespace declarations shadowed inside retained markup', () => {
        const xaml = entryPageExcerpt
          .replace(
            'xmlns:controls="clr-namespace:Maui.Controls.Sample.Pages"',
            'xmlns:controls="clr-namespace:Maui.Controls.Sample.Pages"\n      xmlns:local="clr-namespace:Root"'
          )
          .replace(
            '<Label Text="Password" Style="{StaticResource Headline}" />',
            `<Label Text="Password">
              <Label.Behaviors xmlns:local="clr-namespace:Nested">
                <local:NestedBehavior />
              </Label.Behaviors>
            </Label>`
          );

        const generated = generator.generateXaml(parser.parseXaml(xaml));

        expect(generated).toContain('xmlns:local="clr-namespace:Root"');
        expect(generated).toContain(
          '<Label.Behaviors xmlns:local="clr-namespace:Nested">'
        );
        expect(generated).toContain('<local:NestedBehavior');
      });
    });
  });
});
