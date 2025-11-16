<script setup lang="ts">
useHead({
  title: 'Home',
});
definePageMeta({ auth: false })

const { signIn, token, refreshToken, data, status, lastRefreshedAt, signOut } = useAuth()

</script>
<template>
  <div>
    <h1>Home</h1>
    <div v-if="status === 'authenticated'">
      <p>Welcome, {{ data?.name }}!</p>
      <button @click="signOut()">Logout</button>
    </div>
    <div v-else>
      <p>You are not logged in</p>
      <NuxtLink to="/login">Login</NuxtLink>
    </div>

    <pre>Status: <span data-testid="status">{{ status }}</span></pre>
    <pre>Data: {{ data || 'no session data present, are you logged in?' }}</pre>
    <pre>Last refreshed at: {{ lastRefreshedAt || 'no refresh happened' }}</pre>
    <pre>JWT token: {{ token || 'no token present, are you logged in?' }}</pre>
    <pre>Refresh token: {{ refreshToken || 'N/A' }}</pre>
  </div>
</template>
