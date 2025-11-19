<script setup lang="ts">
useHead({
  title: 'Create Account',
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
const confirmPassword = ref('')
const isLoading = ref(false)
const errors = ref<string[]>([])
const response = ref()

const passwordsMatch = computed(() => {
  return password.value && confirmPassword.value && password.value === confirmPassword.value
})

const isWeakPassword = computed(() => {
  return password.value.length > 0 && password.value.length < 6
})

async function register() {
  errors.value = []
  
  // Client-side validation
  if (!username.value) {
    errors.value.push('Username is required')
  }
  if (!email.value) {
    errors.value.push('Email is required')
  }
  if (!password.value) {
    errors.value.push('Password is required')
  }
  if (password.value !== confirmPassword.value) {
    errors.value.push('Passwords do not match')
  }
  if (password.value.length < 6) {
    errors.value.push('Password must be at least 6 characters long')
  }
  
  if (errors.value.length > 0) {
    return
  }

  try {
    isLoading.value = true
    response.value = await signUp({
      username: username.value,
      email: email.value,
      password: password.value
    }, { preventLoginFlow: true })
  } catch (error: any) {
    console.error('*** Registration error:')
    console.log('- Status:', error.status)
    console.log('- Message:', error.message) 
    console.log('- Data:', error.data)
    console.log('- Full error object:', error)
    
    // Handle ProblemDetails format
    const title = error.data?.title ?? "Registration failed"
    const detail = error.data?.detail ?? error.message ?? 'Please try again'
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
          <h3 class="card-title mb-0">Create Account</h3>
        </div>
        <div class="card-body">
          
          <!-- Success Message -->
          <div v-if="response && !response.error" class="alert alert-success" data-test-id="Success">
            Account created successfully! You can now sign in.
          </div>

          <!-- Registration Form -->
          <form v-if="!response || response.error" @submit.prevent="register" data-test-id="RegisterForm">
            
            <!-- Error Display -->
            <div v-if="errors.length > 0 || (response && response.error)" class="alert alert-danger" data-test-id="Errors">
              <ul class="mb-0">
                <li v-for="error in errors" :key="error">{{ error }}</li>
                <li v-if="response && response.error">{{ response.error }}</li>
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
                placeholder="Choose a username"
                :disabled="isLoading"
                required
                autocomplete="username"
              />
            </div>

            <!-- Email Field -->
            <div class="mb-3">
              <label for="email" class="form-label">Email Address</label>
              <input
                id="email"
                v-model="email"
                type="email"
                class="form-control"
                data-test-id="email"
                placeholder="Enter your email"
                :disabled="isLoading"
                required
                autocomplete="email"
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
                :class="{ 'is-invalid': isWeakPassword }"
                data-test-id="password"
                placeholder="Enter a secure password"
                :disabled="isLoading"
                required
                autocomplete="new-password"
              />
              <div v-if="isWeakPassword" class="invalid-feedback">
                Password must be at least 6 characters long
              </div>
            </div>

            <!-- Confirm Password Field -->
            <div class="mb-3">
              <label for="confirmPassword" class="form-label">Confirm Password</label>
              <input
                id="confirmPassword"
                v-model="confirmPassword"
                type="password"
                class="form-control"
                :class="{ 'is-invalid': confirmPassword && !passwordsMatch }"
                data-test-id="confirmPassword"
                placeholder="Confirm your password"
                :disabled="isLoading"
                required
                autocomplete="new-password"
              />
              <div v-if="confirmPassword && !passwordsMatch" class="invalid-feedback">
                Passwords do not match
              </div>
            </div>

            <!-- Submit Button -->
            <div class="d-grid mb-3">
              <button
                type="submit"
                class="btn btn-primary"
                data-test-id="Register"
                :disabled="isLoading"
              >
                <span v-if="isLoading" class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                {{ isLoading ? 'Creating Account...' : 'Create Account' }}
              </button>
            </div>

            <!-- Login Link -->
            <div class="text-center">
              <p class="mb-0">
                Already have an account? 
                <NuxtLink to="/login" class="text-decoration-none">Sign in here</NuxtLink>
              </p>
            </div>

          </form>

          <!-- Success Actions -->
          <div v-if="response && !response.error" class="text-center">
            <NuxtLink to="/login" class="btn btn-primary">
              Sign In Now
            </NuxtLink>
          </div>

        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.min-vh-100 {
  min-height: 100vh;
}

.card {
  transition: transform 0.2s ease-in-out;
}

.card:hover {
  transform: translateY(-2px);
}

.form-control:focus {
  border-color: #198754;
  box-shadow: 0 0 0 0.2rem rgba(25, 135, 84, 0.25);
}

.btn-success {
  background: linear-gradient(45deg, #198754, #20c997);
  border: none;
}

.btn-success:hover:not(:disabled) {
  background: linear-gradient(45deg, #157347, #1ea085);
  transform: translateY(-1px);
}

.btn-success:disabled {
  opacity: 0.6;
  transform: none;
}

.spinner-border-sm {
  width: 1rem;
  height: 1rem;
}

.bg-light {
  background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%) !important;
}

.shadow-lg {
  box-shadow: 0 1rem 3rem rgba(0, 0, 0, 0.175) !important;
}

.is-valid {
  border-color: #198754;
}

.is-invalid {
  border-color: #dc3545;
}
</style>
