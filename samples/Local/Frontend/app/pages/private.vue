<script setup lang="ts">
/**
 * Private Page
 * This page is only accessible to authenticated users.
 *
 * It uses the NuxtIdentityUserInfo component to display user information.
 *
 * WARNING: This does seem to display a bug!
 * REPRO STEPS:
 * 1. Login as a user
 * 2. Navigate to the Private page
 * 3. Refresh the browser
 * EXPECTED BEHAVIOR:
 * The user information should be displayed correctly.
 * ACTUAL BEHAVIOR:
 * Nuxt error display is shown: "[nuxt] A composable that requires access to the Nuxt instance was called outside of a plugin, Nuxt hook, Nuxt middleware, or Vue setup function. This is probably not a Nuxt bug. Find out more at `https://nuxt.com/docs/4.x/guide/concepts/auto-imports#vue-and-nuxt-composables`."
 * Address bar contains http://localhost:3000/login?redirect=/private
 *
 * REPRO STEPS 2:
 * 1. Login as a user
 * 2. Type in the URL for the Private page directly (do not navigate via link)
 * ACTUAL BEHAVIOR:
 * Same as above
 */

useHead({
  title: 'Private',
});
</script>
<template>
  <div>
    <h1>Private Page</h1>
    <p class="mb-3">This page is only accessible to authenticated users.</p>
    <AuthStatus />
</div>
</template>

