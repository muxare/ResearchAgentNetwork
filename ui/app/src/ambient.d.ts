declare module 'svelte-dnd-action' {
  // Minimal typings to satisfy TS in Svelte 5 projects
  export const dndzone: (
    node: HTMLElement,
    options?: any
  ) => { destroy(): void; update(options?: any): void };
}

