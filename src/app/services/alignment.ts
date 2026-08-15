import { Injectable } from '@angular/core';
import { ElementService } from './element';
import { MauiElement, ElementType } from '../models/maui-element';

export type AlignMode = 'left' | 'center' | 'right' | 'top' | 'middle' | 'bottom';
export type DistributeMode = 'horizontal' | 'vertical';

export interface AlignmentGuide {
  orientation: 'vertical' | 'horizontal';
  /** Canvas relative position of the guide line. */
  position: number;
}

interface Box {
  element: MauiElement;
  x: number;
  y: number;
  width: number;
  height: number;
}

/**
 * Aligns, distributes and snaps elements that live in an AbsoluteLayout,
 * and computes the smart guides shown while dragging.
 */
@Injectable({ providedIn: 'root' })
export class AlignmentService {
  /** Distance (px) within which a dragged edge snaps to a sibling edge. */
  readonly SNAP_THRESHOLD = 6;

  constructor(private elementService: ElementService) {}

  /** Only absolutely positioned elements can be aligned. */
  canAlign(elements: MauiElement[]): boolean {
    return this.positionable(elements).length > 1;
  }

  align(elements: MauiElement[], mode: AlignMode): void {
    const boxes = this.toBoxes(this.positionable(elements));
    if (boxes.length < 2) {
      return;
    }

    const left = Math.min(...boxes.map(box => box.x));
    const right = Math.max(...boxes.map(box => box.x + box.width));
    const top = Math.min(...boxes.map(box => box.y));
    const bottom = Math.max(...boxes.map(box => box.y + box.height));

    this.elementService.runAsSingleChange(() => {
      for (const box of boxes) {
        const patch: { x?: number; y?: number } = {};
        switch (mode) {
          case 'left':
            patch.x = left;
            break;
          case 'right':
            patch.x = right - box.width;
            break;
          case 'center':
            patch.x = Math.round((left + right) / 2 - box.width / 2);
            break;
          case 'top':
            patch.y = top;
            break;
          case 'bottom':
            patch.y = bottom - box.height;
            break;
          case 'middle':
            patch.y = Math.round((top + bottom) / 2 - box.height / 2);
            break;
        }
        this.elementService.updateElementProperties(box.element, patch, { recordHistory: false });
      }
    });
  }

  /** Spreads elements so the gaps between them are equal. */
  distribute(elements: MauiElement[], mode: DistributeMode): void {
    const boxes = this.toBoxes(this.positionable(elements));
    if (boxes.length < 3) {
      return;
    }

    const horizontal = mode === 'horizontal';
    const sorted = [...boxes].sort((a, b) => (horizontal ? a.x - b.x : a.y - b.y));
    const first = sorted[0];
    const last = sorted[sorted.length - 1];

    const start = horizontal ? first.x + first.width : first.y + first.height;
    const end = horizontal ? last.x : last.y;
    const occupied = sorted
      .slice(1, -1)
      .reduce((total, box) => total + (horizontal ? box.width : box.height), 0);
    const gap = (end - start - occupied) / (sorted.length - 1);

    this.elementService.runAsSingleChange(() => {
      let cursor = start + gap;
      for (const box of sorted.slice(1, -1)) {
        this.elementService.updateElementProperties(
          box.element,
          horizontal ? { x: Math.round(cursor) } : { y: Math.round(cursor) },
          { recordHistory: false }
        );
        cursor += (horizontal ? box.width : box.height) + gap;
      }
    });
  }

  /** Rounds a coordinate to the nearest grid intersection. */
  snapToGrid(value: number, gridSize: number): number {
    if (gridSize <= 1) {
      return Math.round(value);
    }
    return Math.round(value / gridSize) * gridSize;
  }

  /**
   * Returns the guides a dragged element aligns with, plus the snapped position.
   * Edges and centres of every sibling are considered.
   */
  computeGuides(dragged: MauiElement, x: number, y: number): { x: number; y: number; guides: AlignmentGuide[] } {
    const parent = dragged.parent;
    if (!parent) {
      return { x, y, guides: [] };
    }

    const width = dragged.properties.width || 0;
    const height = dragged.properties.height || 0;
    const siblings = parent.children.filter(child => child !== dragged);

    const verticalTargets: number[] = [];
    const horizontalTargets: number[] = [];

    for (const sibling of siblings) {
      const sx = sibling.properties.x || 0;
      const sy = sibling.properties.y || 0;
      const sw = sibling.properties.width || 0;
      const sh = sibling.properties.height || 0;
      verticalTargets.push(sx, sx + sw / 2, sx + sw);
      horizontalTargets.push(sy, sy + sh / 2, sy + sh);
    }

    // The parent's own edges and centre are guides too
    const parentWidth = parent.properties.width || 0;
    const parentHeight = parent.properties.height || 0;
    verticalTargets.push(0, parentWidth / 2, parentWidth);
    horizontalTargets.push(0, parentHeight / 2, parentHeight);

    const guides: AlignmentGuide[] = [];
    const horizontalMatch = this.findSnap([x, x + width / 2, x + width], verticalTargets);
    const verticalMatch = this.findSnap([y, y + height / 2, y + height], horizontalTargets);

    let snappedX = x;
    let snappedY = y;

    if (horizontalMatch) {
      snappedX = x + horizontalMatch.delta;
      guides.push({ orientation: 'vertical', position: horizontalMatch.target });
    }

    if (verticalMatch) {
      snappedY = y + verticalMatch.delta;
      guides.push({ orientation: 'horizontal', position: verticalMatch.target });
    }

    return { x: snappedX, y: snappedY, guides };
  }

  private findSnap(candidates: number[], targets: number[]): { target: number; delta: number } | null {
    let best: { target: number; delta: number } | null = null;

    for (const candidate of candidates) {
      for (const target of targets) {
        const delta = target - candidate;
        if (Math.abs(delta) <= this.SNAP_THRESHOLD && (!best || Math.abs(delta) < Math.abs(best.delta))) {
          best = { target, delta };
        }
      }
    }

    return best;
  }

  private positionable(elements: MauiElement[]): MauiElement[] {
    return elements.filter(element => element.parent?.type === ElementType.AbsoluteLayout);
  }

  private toBoxes(elements: MauiElement[]): Box[] {
    return elements.map(element => ({
      element,
      x: element.properties.x || 0,
      y: element.properties.y || 0,
      width: element.properties.width || 0,
      height: element.properties.height || 0
    }));
  }
}
