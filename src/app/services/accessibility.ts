import { Injectable } from '@angular/core';
import { ElementType, MauiElement } from '../models/maui-element';

/** A problem found on a single element. */
export interface AccessibilityIssue {
  elementId: string;
  elementName: string;
  kind: 'missing-description' | 'low-contrast';
  message: string;
}

/**
 * MAUI accepts named colours as well as hex, and the canvas renders them, so
 * the contrast check has to understand at least the common ones. Anything not
 * listed here is skipped rather than guessed at.
 */
const NAMED_COLORS: Record<string, string> = {
  black: '#000000',
  white: '#ffffff',
  red: '#ff0000',
  green: '#008000',
  blue: '#0000ff',
  yellow: '#ffff00',
  cyan: '#00ffff',
  magenta: '#ff00ff',
  gray: '#808080',
  grey: '#808080',
  silver: '#c0c0c0',
  maroon: '#800000',
  olive: '#808000',
  lime: '#00ff00',
  teal: '#008080',
  navy: '#000080',
  purple: '#800080',
  orange: '#ffa500'
};

/** WCAG 2.1 AA requires 4.5:1 for normal text and 3:1 for large text. */
const AA_NORMAL = 4.5;
const AA_LARGE = 3;

/** WCAG counts >=18pt, or >=14pt bold, as large text. */
const LARGE_TEXT_SIZE = 18;
const LARGE_BOLD_TEXT_SIZE = 14;

@Injectable({ providedIn: 'root' })
export class AccessibilityService {
  /** Element types that convey meaning and so need a description for screen readers. */
  private static readonly NEEDS_DESCRIPTION = [ElementType.Image, ElementType.Path];

  /** Walks the tree and reports everything worth fixing. */
  audit(root: MauiElement): AccessibilityIssue[] {
    const issues: AccessibilityIssue[] = [];
    this.visit(root, issues);
    return issues;
  }

  private visit(element: MauiElement, issues: AccessibilityIssue[]): void {
    issues.push(...this.inspect(element));
    element.children.forEach(child => this.visit(child, issues));
  }

  /** The issues on one element, ignoring its children. */
  inspect(element: MauiElement): AccessibilityIssue[] {
    const issues: AccessibilityIssue[] = [];
    const props = element.properties;
    const identity = { elementId: element.id, elementName: element.name };

    if (
      AccessibilityService.NEEDS_DESCRIPTION.includes(element.type) &&
      !props.semanticDescription
    ) {
      issues.push({
        ...identity,
        kind: 'missing-description',
        message: `${element.name} has no SemanticProperties.Description, so a screen reader cannot describe it.`
      });
    }

    const ratio = this.contrastOf(element);
    if (ratio !== null) {
      const required = this.isLargeText(element) ? AA_LARGE : AA_NORMAL;
      if (ratio < required) {
        issues.push({
          ...identity,
          kind: 'low-contrast',
          message: `${element.name} has a contrast ratio of ${ratio.toFixed(2)}:1, below the WCAG AA minimum of ${required}:1.`
        });
      }
    }

    return issues;
  }

  /**
   * The contrast between an element's text and its background, or null when
   * that cannot be determined -- there is no text, or a colour is missing or
   * not one we can resolve. Returning null rather than a guess keeps the panel
   * from reporting confident nonsense.
   */
  contrastOf(element: MauiElement): number | null {
    const props = element.properties;
    if (!props.text) {
      return null;
    }

    const foreground = this.toRgb(props.textColor);
    const background = this.toRgb(props.backgroundColor) ?? this.inheritedBackground(element);
    if (!foreground || !background) {
      return null;
    }

    return this.contrastRatio(foreground, background);
  }

  /** Text sits on whatever the nearest ancestor with a background paints. */
  private inheritedBackground(element: MauiElement): [number, number, number] | null {
    for (let current = element.parent; current; current = current.parent) {
      const color = this.toRgb(current.properties.backgroundColor);
      if (color) {
        return color;
      }
    }
    return null;
  }

  private isLargeText(element: MauiElement): boolean {
    const size = element.properties.fontSize ?? 14;
    const bold = String(element.properties.fontAttributes ?? '').toLowerCase().includes('bold');
    return size >= LARGE_TEXT_SIZE || (bold && size >= LARGE_BOLD_TEXT_SIZE);
  }

  /** Parses #rgb, #rrggbb, #aarrggbb (MAUI's order) and the named colours above. */
  toRgb(value?: string): [number, number, number] | null {
    if (!value) {
      return null;
    }

    const named = NAMED_COLORS[value.trim().toLowerCase()];
    const hex = (named ?? value).trim();
    if (!hex.startsWith('#')) {
      return null;
    }

    const digits = hex.slice(1);
    if (!/^[0-9a-f]+$/i.test(digits)) {
      return null;
    }

    if (digits.length === 3) {
      return [
        parseInt(digits[0] + digits[0], 16),
        parseInt(digits[1] + digits[1], 16),
        parseInt(digits[2] + digits[2], 16)
      ];
    }
    if (digits.length === 6) {
      return [
        parseInt(digits.slice(0, 2), 16),
        parseInt(digits.slice(2, 4), 16),
        parseInt(digits.slice(4, 6), 16)
      ];
    }
    // MAUI writes #AARRGGBB, so the alpha comes first and is dropped here.
    if (digits.length === 8) {
      return [
        parseInt(digits.slice(2, 4), 16),
        parseInt(digits.slice(4, 6), 16),
        parseInt(digits.slice(6, 8), 16)
      ];
    }

    return null;
  }

  /** WCAG relative luminance. */
  private luminance([r, g, b]: [number, number, number]): number {
    const channel = (value: number) => {
      const scaled = value / 255;
      return scaled <= 0.03928 ? scaled / 12.92 : Math.pow((scaled + 0.055) / 1.055, 2.4);
    };
    return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
  }

  contrastRatio(a: [number, number, number], b: [number, number, number]): number {
    const lighter = Math.max(this.luminance(a), this.luminance(b));
    const darker = Math.min(this.luminance(a), this.luminance(b));
    return (lighter + 0.05) / (darker + 0.05);
  }
}
