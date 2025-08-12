<script lang="ts">
  import '../app.css';
  import favicon from '$lib/assets/favicon.svg';
  import Toaster from '$lib/components/Toaster.svelte';
  import VersionBanner from '$lib/components/VersionBanner.svelte';

  let { children } = $props();
</script>

<svelte:head>
	<link rel="icon" href={favicon} />
</svelte:head>

<VersionBanner />
{@render children?.()}
<Toaster />
