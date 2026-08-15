import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { ElementService } from './element';
import { XamlParserService } from './xaml-parser';
import { MauiElement, ElementType } from '../models/maui-element';

export interface ComponentTemplate {
  id: string;
  name: string;
  /** Serialized MauiElement array. */
  payload: string;
  createdAt: number;
}

export interface StarterPage {
  id: string;
  name: string;
  description: string;
  xaml: string;
}

export const STARTER_PAGES: StarterPage[] = [
  {
    id: 'login',
    name: 'Login page',
    description: 'Title, email and password entries with a sign in button',
    xaml: `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    <AbsoluteLayout x:Name="LoginPage" WidthRequest="390" HeightRequest="844" BackgroundColor="#ffffff">
        <Label x:Name="Title" Text="Welcome back" AbsoluteLayout.LayoutBounds="24,80,300,40" WidthRequest="300" HeightRequest="40" FontSize="26" FontAttributes="Bold" TextColor="#111827" />
        <Label x:Name="Subtitle" Text="Sign in to continue" AbsoluteLayout.LayoutBounds="24,124,300,24" WidthRequest="300" HeightRequest="24" FontSize="14" TextColor="#6b7280" />
        <Entry x:Name="EmailEntry" Placeholder="Email" Text="{Binding Email}" AbsoluteLayout.LayoutBounds="24,190,342,44" WidthRequest="342" HeightRequest="44" BackgroundColor="#ffffff" />
        <Entry x:Name="PasswordEntry" Placeholder="Password" Text="{Binding Password}" AbsoluteLayout.LayoutBounds="24,248,342,44" WidthRequest="342" HeightRequest="44" BackgroundColor="#ffffff" />
        <Switch x:Name="RememberSwitch" AbsoluteLayout.LayoutBounds="24,308,60,32" WidthRequest="60" HeightRequest="32" IsToggled="{Binding RememberMe}" />
        <Label x:Name="RememberLabel" Text="Remember me" AbsoluteLayout.LayoutBounds="96,312,200,24" WidthRequest="200" HeightRequest="24" FontSize="14" TextColor="#374151" />
        <Button x:Name="SignInButton" Text="Sign in" Command="{Binding SignInCommand}" AbsoluteLayout.LayoutBounds="24,364,342,48" WidthRequest="342" HeightRequest="48" BackgroundColor="#2563eb" TextColor="#ffffff" />
        <ActivityIndicator x:Name="Busy" AbsoluteLayout.LayoutBounds="185,430,40,40" WidthRequest="40" HeightRequest="40" IsRunning="{Binding IsBusy}" />
    </AbsoluteLayout>
</ContentPage>`
  },
  {
    id: 'list',
    name: 'List page',
    description: 'Search bar over a bound CollectionView',
    xaml: `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    <AbsoluteLayout x:Name="ListPage" WidthRequest="390" HeightRequest="844" BackgroundColor="#ffffff">
        <Label x:Name="ListTitle" Text="Inbox" AbsoluteLayout.LayoutBounds="24,56,200,36" WidthRequest="200" HeightRequest="36" FontSize="24" FontAttributes="Bold" TextColor="#111827" />
        <SearchBar x:Name="Search" Placeholder="Search messages" Text="{Binding Query}" AbsoluteLayout.LayoutBounds="24,104,342,44" WidthRequest="342" HeightRequest="44" />
        <CollectionView x:Name="Items" ItemsSource="{Binding Messages}" AbsoluteLayout.LayoutBounds="24,164,342,600" WidthRequest="342" HeightRequest="600">
            <CollectionView.ItemTemplate>
                <DataTemplate>
                    <Label x:Name="ItemLabel" Text="{Binding Title}" WidthRequest="342" HeightRequest="44" FontSize="15" TextColor="#111827" />
                </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>
    </AbsoluteLayout>
</ContentPage>`
  },
  {
    id: 'profile',
    name: 'Profile card',
    description: 'Bordered profile card with avatar, name and action',
    xaml: `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    <AbsoluteLayout x:Name="ProfilePage" WidthRequest="390" HeightRequest="844" BackgroundColor="#f3f4f6">
        <Border x:Name="Card" AbsoluteLayout.LayoutBounds="24,80,342,220" WidthRequest="342" HeightRequest="220" BackgroundColor="#ffffff" Stroke="#e5e7eb" StrokeThickness="1" StrokeShape="RoundRectangle 16" />
        <Image x:Name="Avatar" AbsoluteLayout.LayoutBounds="48,104,80,80" WidthRequest="80" HeightRequest="80" />
        <Label x:Name="Name" Text="{Binding DisplayName}" AbsoluteLayout.LayoutBounds="144,110,200,28" WidthRequest="200" HeightRequest="28" FontSize="20" FontAttributes="Bold" TextColor="#111827" />
        <Label x:Name="Role" Text="{Binding Role}" AbsoluteLayout.LayoutBounds="144,142,200,22" WidthRequest="200" HeightRequest="22" FontSize="14" TextColor="#6b7280" />
        <ProgressBar x:Name="Completion" Progress="0.7" AbsoluteLayout.LayoutBounds="48,210,294,12" WidthRequest="294" HeightRequest="12" />
        <Button x:Name="EditButton" Text="Edit profile" Command="{Binding EditCommand}" AbsoluteLayout.LayoutBounds="48,240,294,44" WidthRequest="294" HeightRequest="44" BackgroundColor="#111827" TextColor="#ffffff" />
    </AbsoluteLayout>
</ContentPage>`
  },
  {
    id: 'settings',
    name: 'Settings page',
    description: 'Grid of setting rows with switches and a slider',
    xaml: `<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    <AbsoluteLayout x:Name="SettingsPage" WidthRequest="390" HeightRequest="844" BackgroundColor="#ffffff">
        <Label x:Name="SettingsTitle" Text="Settings" AbsoluteLayout.LayoutBounds="24,56,200,36" WidthRequest="200" HeightRequest="36" FontSize="24" FontAttributes="Bold" TextColor="#111827" />
        <Label x:Name="NotificationsLabel" Text="Notifications" AbsoluteLayout.LayoutBounds="24,120,220,24" WidthRequest="220" HeightRequest="24" FontSize="16" TextColor="#374151" />
        <Switch x:Name="NotificationsSwitch" IsToggled="{Binding NotificationsEnabled}" AbsoluteLayout.LayoutBounds="306,116,60,32" WidthRequest="60" HeightRequest="32" />
        <Label x:Name="DarkModeLabel" Text="Dark mode" AbsoluteLayout.LayoutBounds="24,168,220,24" WidthRequest="220" HeightRequest="24" FontSize="16" TextColor="#374151" />
        <Switch x:Name="DarkModeSwitch" IsToggled="{Binding DarkMode}" AbsoluteLayout.LayoutBounds="306,164,60,32" WidthRequest="60" HeightRequest="32" />
        <Label x:Name="VolumeLabel" Text="Volume" AbsoluteLayout.LayoutBounds="24,216,220,24" WidthRequest="220" HeightRequest="24" FontSize="16" TextColor="#374151" />
        <Slider x:Name="VolumeSlider" Minimum="0" Maximum="100" Value="{Binding Volume}" AbsoluteLayout.LayoutBounds="24,248,342,32" WidthRequest="342" HeightRequest="32" />
    </AbsoluteLayout>
</ContentPage>`
  }
];

/**
 * Copy/cut/paste of canvas elements plus reusable component templates and
 * starter pages. Templates are stored in localStorage so they survive reloads.
 */
@Injectable({ providedIn: 'root' })
export class ClipboardService {
  private static readonly TEMPLATE_KEY = 'maui-designer.templates';
  private static readonly CLIPBOARD_KEY = 'maui-designer.clipboard';

  private buffer: string | null = null;
  private templatesSubject = new BehaviorSubject<ComponentTemplate[]>(this.readTemplates());
  templates$ = this.templatesSubject.asObservable();

  constructor(
    private elementService: ElementService,
    private xamlParser: XamlParserService
  ) {
    this.buffer = this.readClipboard();
  }

  // --- Clipboard --------------------------------------------------------------

  copy(elements: MauiElement[]): number {
    const copyable = elements.filter(element => element.parent);
    if (copyable.length === 0) {
      return 0;
    }
    this.buffer = this.elementService.serializeElements(copyable);
    this.writeClipboard(this.buffer);
    return copyable.length;
  }

  cut(elements: MauiElement[]): number {
    const count = this.copy(elements);
    if (count > 0) {
      this.elementService.removeSelectedElements();
    }
    return count;
  }

  hasContent(): boolean {
    return !!this.buffer;
  }

  /** Pastes into the given parent, the selected layout, or the root. */
  paste(parent?: MauiElement): MauiElement[] {
    if (!this.buffer) {
      return [];
    }
    return this.elementService.insertSerializedElements(this.buffer, parent || this.resolveTargetParent(), 16);
  }

  // --- Component templates ----------------------------------------------------

  saveTemplate(name: string, elements: MauiElement[]): ComponentTemplate | null {
    const savable = elements.filter(element => element.parent);
    if (savable.length === 0 || !name.trim()) {
      return null;
    }

    const template: ComponentTemplate = {
      id: `template_${Date.now()}_${Math.round(Math.random() * 1000)}`,
      name: name.trim(),
      payload: this.elementService.serializeElements(savable),
      createdAt: Date.now()
    };

    const templates = [...this.templatesSubject.value, template];
    this.writeTemplates(templates);
    return template;
  }

  deleteTemplate(id: string): void {
    this.writeTemplates(this.templatesSubject.value.filter(template => template.id !== id));
  }

  getTemplates(): ComponentTemplate[] {
    return this.templatesSubject.value;
  }

  insertTemplate(id: string, parent?: MauiElement): MauiElement[] {
    const template = this.templatesSubject.value.find(candidate => candidate.id === id);
    if (!template) {
      return [];
    }
    return this.elementService.insertSerializedElements(template.payload, parent || this.resolveTargetParent(), 0);
  }

  // --- Starter pages ----------------------------------------------------------

  get starterPages(): StarterPage[] {
    return STARTER_PAGES;
  }

  /** Replaces the whole design with a starter page (undoable). */
  applyStarterPage(id: string): boolean {
    const page = STARTER_PAGES.find(candidate => candidate.id === id);
    if (!page) {
      return false;
    }
    try {
      const root = this.xamlParser.parseXaml(page.xaml);
      this.elementService.setRootElement(root);
      return true;
    } catch {
      return false;
    }
  }

  // --- Internals --------------------------------------------------------------

  private resolveTargetParent(): MauiElement {
    const selected = this.elementService.getSelectedElement();
    if (selected && this.canHaveChildren(selected.type)) {
      return selected;
    }
    if (selected?.parent) {
      return selected.parent;
    }
    return this.elementService.getRootElement();
  }

  private canHaveChildren(type: ElementType): boolean {
    return [
      ElementType.StackLayout,
      ElementType.VerticalStackLayout,
      ElementType.Grid,
      ElementType.AbsoluteLayout,
      ElementType.Frame,
      ElementType.Border,
      ElementType.ScrollView,
      ElementType.CollectionView
    ].includes(type);
  }

  private readTemplates(): ComponentTemplate[] {
    try {
      const stored = localStorage.getItem(ClipboardService.TEMPLATE_KEY);
      const parsed = stored ? JSON.parse(stored) : [];
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }

  private writeTemplates(templates: ComponentTemplate[]): void {
    this.templatesSubject.next(templates);
    try {
      localStorage.setItem(ClipboardService.TEMPLATE_KEY, JSON.stringify(templates));
    } catch {
      // storage is optional
    }
  }

  private readClipboard(): string | null {
    try {
      return localStorage.getItem(ClipboardService.CLIPBOARD_KEY);
    } catch {
      return null;
    }
  }

  private writeClipboard(payload: string): void {
    try {
      localStorage.setItem(ClipboardService.CLIPBOARD_KEY, payload);
    } catch {
      // storage is optional
    }
  }
}
