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

const { signIn, status } = useAuth()

const username = ref('')
const password = ref('')
const isLoading = ref(false)
const errors = ref<string[]>([])

const handleLogin = async () => {
  errors.value = []
  
  // Client-side validation
  if (!username.value) {
    errors.value.push('Username is required')
  }
  if (!password.value) {
    errors.value.push('Password is required')
  }
  
  if (errors.value.length > 0) {
    return
  }

  try {
    isLoading.value = true
    await signIn({ 
      username: username.value, 
      password: password.value 
    },{ 
      redirect: true, 
      callbackUrl: '/' 
    })
  } catch (error: any) {
    console.error('*** Login error:')
    console.log('- Status:', error.status)
    console.log('- Message:', error.message) 
    console.log('- Data:', error.data)
    console.log('- Full error object:', error)
    
    // Handle ProblemDetails format
    const title = error.data?.title ?? "Login failed"
    const detail = error.data?.detail ?? error.message ?? 'Please check your credentials'
    errors.value = [`${title}: ${detail}`]
  } finally {
    isLoading.value = false
  }
}
</script>

<template>
  <div class="row justify-content-center">
    <div class="col-md-6 col-lg-4">
      <div class="card shadow">
        <div class="card-header text-center">
          <h3 class="card-title mb-0">Sign In</h3>
        </div>
        <div class="card-body">
          <form @submit.prevent="handleLogin" data-test-id="LoginForm">
            
            <!-- Error Display -->
            <div v-if="errors.length > 0" class="alert alert-danger" data-test-id="Errors">
              <ul class="mb-0">
                <li v-for="error in errors" :key="error">{{ error }}</li>
              </ul>
            </div>

            <!-- Username Field -->
            <div class="mb-3">
              <label for="username" class="form-label">Username</label>
              <input
                id="username"
                v-model="username"
                type="text"
                class="form-control"
                data-test-id="username"
                placeholder="Enter your username"
                :disabled="isLoading"
                required
                autocomplete="username"
              />
            </div>

            <!-- Password Field -->
            <div class="mb-3">
              <label for="password" class="form-label">Password</label>
              <input
                id="password"
                v-model="password"
                type="password"
                class="form-control"
                data-test-id="password"
                placeholder="Enter your password"
                :disabled="isLoading"
                required
                autocomplete="current-password"
              />
            </div>

            <!-- Submit Button -->
            <div class="d-grid mb-3">
              <button
                type="submit"
                class="btn btn-primary"
                data-test-id="Login"
                :disabled="isLoading"
              >
                <span v-if="isLoading" class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                {{ isLoading ? 'Signing In...' : 'Sign In' }}
              </button>
            </div>

            <!-- Registration Link -->
            <div class="text-center">
              <p class="mb-0">
                Don't have an account? 
                <NuxtLink to="/register" class="text-decoration-none">Create one here</NuxtLink>
              </p>
            </div>

          </form>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.form-control:focus {
  border-color: #0d6efd;
  box-shadow: 0 0 0 0.2rem rgba(13, 110, 253, 0.25);
}

.btn-primary:hover {
  transform: translateY(-1px);
}

.spinner-border-sm {
  width: 1rem;
  height: 1rem;
}

</style>
