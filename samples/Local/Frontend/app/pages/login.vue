<script setup lang="ts">
useHead({
  title: 'Login',
});

definePageMeta({
  auth: {
    unauthenticatedOnly: true,
    navigateAuthenticatedTo: '/'
  }
})

const { signIn, token, data, status, lastRefreshedAt } = useAuth()

const username = ref('')
const password = ref('')

</script>
<template>
  <div>
    <h1>Login</h1>
    <pre>Status: {{ status }}</pre>
    <form @submit.prevent="signIn({ username, password })">
      <input v-model="username" type="text" placeholder="Username">
      <input v-model="password" type="password" placeholder="Password">
      <button type="submit">
        sign in
      </button>
    </form>
  </div>
</template>
