<script lang="ts">
  export let title: string;
  export let subtitle: string | undefined = undefined;
</script>

<div>
  <h1 class="text-2xl md:text-3xl font-bold tracking-tight">{title}</h1>
  {#if subtitle}
    <p class="text-sm text-gray-600">{subtitle}</p>
  {/if}
  
</div>

