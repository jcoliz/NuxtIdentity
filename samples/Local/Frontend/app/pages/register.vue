<script setup lang="ts">
useHead({
  title: 'Register',
});

definePageMeta({
  auth: {
    unauthenticatedOnly: true,
    navigateAuthenticatedTo: '/'
  }
})


const { signUp } = useAuth()

const username = ref('')
const email = ref('')
const password = ref('')
const response = ref()

async function register() {
  try {
    response.value = await signUp({
      username: username.value,
      email: email.value,
      password: password.value
    }, { preventLoginFlow: true })
  }
  catch (error) {
    response.value = { error: 'Failed to sign up: ' + error }
    console.error(error)
  }
}

</script>
<template>
  <div>
    <h1>Register</h1>
    <form @submit.prevent="register">
      <p><i>*password should have at least 6 characters</i></p>
      <input v-model="username" type="text" placeholder="Username" data-testid="register-username">
      <input v-model="email" type="text" placeholder="Email@yourdomain.com" data-testid="register-email">
      <input v-model="password" type="password" placeholder="Password" data-testid="register-password">
      <button type="submit" data-testid="register-submit">
        sign up
      </button>
    </form>
    <div v-if="response">
      <h2>Response</h2>
      <pre>{{ response }}</pre>
    </div>    
  </div>
</template>
