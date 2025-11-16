<script setup lang="ts">
useHead({
  title: 'Home',
});
definePageMeta({ auth: false })

const { signIn, token, refreshToken, data, status, lastRefreshedAt, signOut } = useAuth()

</script>
<template>
  <div>
    <h1 class="mb-3">Home</h1>
    <AuthStatus />
  </div>
</template>
