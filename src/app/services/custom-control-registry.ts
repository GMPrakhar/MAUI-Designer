import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import {
  BUNDLED_MANIFESTS,
  CustomControlDefinition,
  CustomControlLookup,
  CustomControlManifest,
  CustomPropertyDefinition,
  customControlKey
} from '../models/custom-control';
import { ElementProperties, MauiElement } from '../models/maui-element';

/**
 * Holds the manifests that describe third party controls. Manifests can be
 * bundled with the app, imported as JSON (for example generated from a NuGet
 * package), or learned automatically from imported XAML.
 */
@Injectable({ providedIn: 'root' })
export class CustomControlRegistryService {
  private static readonly STORAGE_KEY = 'maui-designer.custom-controls';

  private manifestsSubject = new BehaviorSubject<CustomControlManifest[]>(this.restore());
  manifests$ = this.manifestsSubject.asObservable();

  get manifests(): CustomControlManifest[] {
    return this.manifestsSubject.value;
  }

  /** Every registered control, flattened, for toolbox rendering. */
  get controls(): CustomControlLookup[] {
    return this.manifests.flatMap(manifest =>
      manifest.controls.map(definition => ({ manifest, definition }))
    );
  }

  /** Finds a control by `prefix:Tag`, falling back to a prefix-less match. */
  find(prefix: string | undefined, tag: string): CustomControlLookup | null {
    const byPrefix = this.manifests
      .filter(manifest => !prefix || manifest.xmlns.prefix === prefix)
      .flatMap(manifest => manifest.controls.map(definition => ({ manifest, definition })))
      .find(entry => entry.definition.tag.toLowerCase() === tag.toLowerCase());

    if (byPrefix) {
      return byPrefix;
    }

    return (
      this.controls.find(entry => entry.definition.tag.toLowerCase() === tag.toLowerCase()) || null
    );
  }

  findForElement(element: MauiElement): CustomControlLookup | null {
    const { customTag, customPrefix } = element.properties;
    return customTag ? this.find(customPrefix, customTag) : null;
  }

  /** The properties a custom element can be bound to. */
  bindableProperties(element: MauiElement): string[] {
    const lookup = this.findForElement(element);
    const declared = (lookup?.definition.properties || [])
      .filter(property => property.bindable !== false)
      .map(property => property.name);
    const raw = Object.keys(element.properties.rawAttributes || {});
    return [...new Set([...declared, ...raw])];
  }

  // --- Manifest management ----------------------------------------------------

  /** Adds or replaces a manifest (matched on id, or on namespace prefix). */
  register(manifest: CustomControlManifest): CustomControlManifest {
    const normalized = this.normalize(manifest);
    const existing = this.manifests.find(
      candidate => candidate.id === normalized.id || candidate.xmlns.prefix === normalized.xmlns.prefix
    );

    const next = existing
      ? this.manifests.map(candidate => (candidate === existing ? this.merge(existing, normalized) : candidate))
      : [...this.manifests, normalized];

    this.write(next);
    return this.manifests.find(candidate => candidate.id === normalized.id) || normalized;
  }

  remove(id: string): void {
    this.write(this.manifests.filter(manifest => manifest.id !== id));
  }

  /** Restores the bundled manifests and drops everything else. */
  resetToBundled(): void {
    this.write(BUNDLED_MANIFESTS.map(manifest => this.normalize(manifest)));
  }

  /** Parses a manifest JSON document; accepts a single manifest or an array. */
  import(json: string): CustomControlManifest[] {
    const parsed = JSON.parse(json);
    const candidates: unknown[] = Array.isArray(parsed) ? parsed : [parsed];
    const imported: CustomControlManifest[] = [];

    for (const candidate of candidates) {
      const manifest = candidate as CustomControlManifest;
      if (!manifest || !manifest.xmlns?.prefix || !manifest.xmlns?.uri || !Array.isArray(manifest.controls)) {
        throw new Error('Invalid manifest: expected { package, xmlns: { prefix, uri }, controls: [] }');
      }
      imported.push(this.register(manifest));
    }

    return imported;
  }

  export(): string {
    return JSON.stringify(this.manifests, null, 2);
  }

  /**
   * Registers a control seen in imported XAML so it becomes editable even
   * though no manifest was supplied. Existing manifests are never downgraded.
   */
  learn(prefix: string, uri: string, tag: string, attributes: Record<string, string>): CustomControlLookup {
    const existing = this.find(prefix, tag);
    if (existing && !existing.manifest.learned) {
      return existing;
    }

    const properties: CustomPropertyDefinition[] = Object.keys(attributes).map(name => ({
      name,
      type: this.inferType(attributes[name])
    }));

    const manifest = this.manifests.find(candidate => candidate.xmlns.prefix === prefix);
    const definition: CustomControlDefinition = {
      tag,
      displayName: tag,
      description: `Learned from imported XAML (${prefix ? `${prefix}:` : ''}${tag})`,
      icon: 'extension',
      canHaveChildren: true,
      preview: { kind: 'box', label: tag, icon: 'extension' },
      properties
    };

    if (manifest) {
      const merged: CustomControlManifest = {
        ...manifest,
        controls: manifest.controls.some(control => control.tag === tag)
          ? manifest.controls.map(control =>
              control.tag === tag ? this.mergeDefinition(control, definition) : control
            )
          : [...manifest.controls, definition]
      };
      this.write(this.manifests.map(candidate => (candidate === manifest ? merged : candidate)));
    } else {
      this.write([
        ...this.manifests,
        this.normalize({
          id: `learned-${prefix || 'default'}`,
          package: prefix ? `Imported (${prefix})` : 'Imported controls',
          description: 'Controls discovered in imported XAML',
          xmlns: { prefix, uri },
          controls: [definition],
          learned: true
        })
      ]);
    }

    return this.find(prefix, tag)!;
  }

  // --- Element helpers --------------------------------------------------------

  /** Default designer properties for a control described by a manifest. */
  defaultProperties(lookup: CustomControlLookup): Partial<ElementProperties> {
    const { manifest, definition } = lookup;
    const customValues: Record<string, string> = {};

    for (const property of definition.properties || []) {
      if (property.defaultValue !== undefined) {
        customValues[property.name] = String(property.defaultValue);
      }
    }

    return {
      x: 0,
      y: 0,
      width: definition.defaultWidth ?? 120,
      height: definition.defaultHeight ?? 48,
      isVisible: true,
      isEnabled: true,
      customTag: definition.tag,
      customPrefix: manifest.xmlns.prefix,
      customNamespace: manifest.xmlns.uri,
      customValues,
      rawAttributes: {}
    };
  }

  /** Resolves `{Property}` placeholders in a preview string. */
  interpolate(template: string | undefined, element: MauiElement): string {
    if (!template) {
      return '';
    }
    const values = element.properties.customValues || {};
    const raw = element.properties.rawAttributes || {};
    return template.replace(/\{([^}]+)\}/g, (_, name: string) => values[name] ?? raw[name] ?? '');
  }

  // --- Internals --------------------------------------------------------------

  private inferType(value: string): CustomPropertyDefinition['type'] {
    if (/^(true|false)$/i.test(value)) {
      return 'boolean';
    }
    if (/^-?\d+(\.\d+)?$/.test(value)) {
      return 'number';
    }
    if (/^#[0-9a-f]{3,8}$/i.test(value)) {
      return 'color';
    }
    return 'string';
  }

  private normalize(manifest: CustomControlManifest): CustomControlManifest {
    return {
      ...manifest,
      id: manifest.id || `manifest-${manifest.xmlns.prefix}-${Date.now()}`,
      package: manifest.package || manifest.xmlns.prefix,
      controls: manifest.controls.map(control => ({
        ...control,
        displayName: control.displayName || control.tag,
        icon: control.icon || 'extension',
        properties: control.properties || []
      }))
    };
  }

  /** A supplied manifest always wins over a learned one. */
  private merge(existing: CustomControlManifest, incoming: CustomControlManifest): CustomControlManifest {
    const controls = [...incoming.controls];
    for (const control of existing.controls) {
      if (!controls.some(candidate => candidate.tag === control.tag)) {
        controls.push(control);
      }
    }
    return { ...existing, ...incoming, controls, learned: incoming.learned && existing.learned };
  }

  private mergeDefinition(
    existing: CustomControlDefinition,
    incoming: CustomControlDefinition
  ): CustomControlDefinition {
    const properties = [...(existing.properties || [])];
    for (const property of incoming.properties || []) {
      if (!properties.some(candidate => candidate.name === property.name)) {
        properties.push(property);
      }
    }
    return { ...existing, properties };
  }

  private write(manifests: CustomControlManifest[]): void {
    this.manifestsSubject.next(manifests);
    try {
      localStorage.setItem(CustomControlRegistryService.STORAGE_KEY, JSON.stringify(manifests));
    } catch {
      // storage is optional
    }
  }

  private restore(): CustomControlManifest[] {
    const bundled = BUNDLED_MANIFESTS.map(manifest => this.normalize(manifest));
    try {
      const stored = localStorage.getItem(CustomControlRegistryService.STORAGE_KEY);
      if (!stored) {
        return bundled;
      }
      const parsed = JSON.parse(stored) as CustomControlManifest[];
      if (!Array.isArray(parsed)) {
        return bundled;
      }
      // Bundled manifests are always available, user manifests win on conflict
      const merged = [...parsed.map(manifest => this.normalize(manifest))];
      for (const manifest of bundled) {
        if (!merged.some(candidate => candidate.xmlns.prefix === manifest.xmlns.prefix)) {
          merged.push(manifest);
        }
      }
      return merged;
    } catch {
      return bundled;
    }
  }
}
