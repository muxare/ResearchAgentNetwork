<script lang="ts">
  export let padded: boolean = false;
</script>

<div class={`bg-white/80 border rounded-xl shadow-sm ${padded ? 'p-3' : ''}`}>
  <slot />
  
</div>

