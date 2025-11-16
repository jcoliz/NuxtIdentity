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

    <AuthStatus />
  </div>
</template>
