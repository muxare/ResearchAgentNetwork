/// <reference types="svelte" />
/// <reference types="svelte/elements" />
/// <reference types="@sveltejs/kit" />

declare module 'svelte-dnd-action' {
  // Minimal typings to satisfy TS in Svelte 5 projects
  export const dndzone: (
    node: HTMLElement,
    options?: any
  ) => { destroy(): void; update(options?: any): void };
}

// Svelte 5 runes minimal typings for TS tooling
declare function $state<T>(value: T): T;
declare function $effect(run: () => void | (() => void)): void;
declare function $props<T = any>(): T;

// Fallback shim so generic TS tooling recognizes Svelte's generated namespace
declare global {
  namespace svelteHTML {
    // eslint-disable-next-line @typescript-eslint/no-empty-interface
    interface IntrinsicElements {
      [name: string]: any;
    }
  }
}
