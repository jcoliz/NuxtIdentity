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
const showPassword = ref(false)
const showConfirmPassword = ref(false)
const errorMessage = ref('')
const response = ref()

const passwordsMatch = computed(() => {
  return password.value && confirmPassword.value && password.value === confirmPassword.value
})

const isFormValid = computed(() => {
  return username.value && 
         email.value && 
         password.value.length >= 6 && 
         passwordsMatch.value &&
         email.value.includes('@')
})

async function register() {
  if (!isFormValid.value) {
    errorMessage.value = 'Please fill in all fields correctly'
    return
  }

  try {
    isLoading.value = true
    errorMessage.value = ''
    response.value = await signUp({
      username: username.value,
      email: email.value,
      password: password.value
    }, { preventLoginFlow: true })
  } catch (error: any) {
    console.error('Registration error:', error)
    errorMessage.value = error.message || 'Failed to create account. Please try again.'
    response.value = { error: 'Failed to sign up: ' + error.message }
  } finally {
    isLoading.value = false
  }
}

const togglePasswordVisibility = () => {
  showPassword.value = !showPassword.value
}

const toggleConfirmPasswordVisibility = () => {
  showConfirmPassword.value = !showConfirmPassword.value
}
</script>

<template>
  <div class="min-vh-100 d-flex align-items-center bg-light">
    <div class="container">
      <div class="row justify-content-center">
        <div class="col-md-6 col-lg-5">
          <!-- Registration Card -->
          <div class="card shadow-lg border-0 rounded-lg">
            <div class="card-header bg-success text-white text-center py-4">
              <div class="mb-2">
                <FeatherIcon icon="user-plus" size="48" class="icon-up-1" />
              </div>
              <h3 class="mb-0">Join Us</h3>
              <p class="mb-0 opacity-75">Create your account</p>
            </div>
            
            <div class="card-body p-4">
              <!-- Success Message -->
              <div v-if="response && !response.error" class="alert alert-success d-flex align-items-center mb-3">
                <FeatherIcon icon="check-circle" size="16" class="me-2" />
                Account created successfully! You can now sign in.
              </div>

              <!-- Error Message -->
              <div v-if="errorMessage || (response && response.error)" class="alert alert-danger d-flex align-items-center mb-3">
                <FeatherIcon icon="alert-triangle" size="16" class="me-2" />
                {{ errorMessage || response.error }}
              </div>

              <!-- Registration Form -->
              <form @submit.prevent="register" v-if="!response || response.error">
                <!-- Username Field -->
                <div class="mb-3">
                  <label for="username" class="form-label">
                    <FeatherIcon icon="user" size="16" class="me-1" />
                    Username
                  </label>
                  <input 
                    id="username"
                    v-model="username" 
                    type="text" 
                    class="form-control form-control-lg"
                    placeholder="Choose a username"
                    :disabled="isLoading"
                    required
                    autocomplete="username"
                    data-testid="register-username"
                  >
                </div>

                <!-- Email Field -->
                <div class="mb-3">
                  <label for="email" class="form-label">
                    <FeatherIcon icon="mail" size="16" class="me-1" />
                    Email Address
                  </label>
                  <input 
                    id="email"
                    v-model="email" 
                    type="email" 
                    class="form-control form-control-lg"
                    :class="{ 'is-valid': email && email.includes('@'), 'is-invalid': email && !email.includes('@') }"
                    placeholder="Enter your email"
                    :disabled="isLoading"
                    required
                    autocomplete="email"
                    data-testid="register-email"
                  >
                </div>

                <!-- Password Field -->
                <div class="mb-3">
                  <label for="password" class="form-label">
                    <FeatherIcon icon="lock" size="16" class="me-1" />
                    Password
                  </label>
                  <div class="input-group">
                    <input 
                      id="password"
                      v-model="password" 
                      :type="showPassword ? 'text' : 'password'" 
                      class="form-control form-control-lg"
                      :class="{ 'is-valid': password.length >= 6, 'is-invalid': password && password.length < 6 }"
                      placeholder="Create a password"
                      :disabled="isLoading"
                      required
                      autocomplete="new-password"
                      data-testid="register-password"
                    >
                    <button 
                      type="button" 
                      class="btn btn-outline-secondary"
                      @click="togglePasswordVisibility"
                      :disabled="isLoading"
                      tabindex="-1"
                    >
                      <FeatherIcon :icon="showPassword ? 'eye-off' : 'eye'" size="16" />
                    </button>
                  </div>
                  <small class="text-muted">
                    <em>Password should have at least 6 characters</em>
                  </small>
                </div>

                <!-- Confirm Password Field -->
                <div class="mb-4">
                  <label for="confirmPassword" class="form-label">
                    <FeatherIcon icon="lock" size="16" class="me-1" />
                    Confirm Password
                  </label>
                  <div class="input-group">
                    <input 
                      id="confirmPassword"
                      v-model="confirmPassword" 
                      :type="showConfirmPassword ? 'text' : 'password'" 
                      class="form-control form-control-lg"
                      :class="{ 'is-valid': passwordsMatch, 'is-invalid': confirmPassword && !passwordsMatch }"
                      placeholder="Confirm your password"
                      :disabled="isLoading"
                      required
                      autocomplete="new-password"
                    >
                    <button 
                      type="button" 
                      class="btn btn-outline-secondary"
                      @click="toggleConfirmPasswordVisibility"
                      :disabled="isLoading"
                      tabindex="-1"
                    >
                      <FeatherIcon :icon="showConfirmPassword ? 'eye-off' : 'eye'" size="16" />
                    </button>
                  </div>
                  <div v-if="confirmPassword && !passwordsMatch" class="text-danger mt-1">
                    <small><FeatherIcon icon="alert-circle" size="14" class="me-1" />Passwords do not match</small>
                  </div>
                  <div v-else-if="passwordsMatch" class="text-success mt-1">
                    <small><FeatherIcon icon="check-circle" size="14" class="me-1" />Passwords match</small>
                  </div>
                </div>

                <!-- Submit Button -->
                <div class="d-grid mb-3">
                  <button 
                    type="submit" 
                    class="btn btn-success btn-lg"
                    :disabled="isLoading || !isFormValid"
                    data-testid="register-submit"
                  >
                    <span v-if="isLoading" class="spinner-border spinner-border-sm me-2" role="status">
                      <span class="visually-hidden">Loading...</span>
                    </span>
                    <span v-if="!isLoading">
                      <FeatherIcon icon="user-plus" size="16" class="me-2" />
                      Create Account
                    </span>
                    <span v-else>
                      Creating Account...
                    </span>
                  </button>
                </div>
              </form>

              <!-- Success Actions -->
              <div v-if="response && !response.error" class="text-center">
                <NuxtLink to="/login" class="btn btn-primary btn-lg">
                  <FeatherIcon icon="log-in" size="16" class="me-2" />
                  Sign In Now
                </NuxtLink>
              </div>

              <!-- Additional Links (only show if not successful) -->
              <div v-if="!response || response.error" class="text-center">
                <p class="text-muted mb-2">Already have an account?</p>
                <NuxtLink to="/login" class="btn btn-outline-success btn-sm">
                  <FeatherIcon icon="log-in" size="16" class="me-1" />
                  Sign In
                </NuxtLink>
              </div>
            </div>

            <!-- Footer -->
            <div class="card-footer bg-light text-center py-3">
              <small class="text-muted">
                <FeatherIcon icon="shield" size="14" class="me-1" />
                Secured with NuxtIdentity
              </small>
            </div>
          </div>

          <!-- Demo Notice -->
          <div class="card mt-3 border-info">
            <div class="card-body bg-info bg-opacity-10 py-2">
              <small class="text-info">
                <FeatherIcon icon="info" size="14" class="me-1" />
                <strong>Demo:</strong> Create an account to test the authentication flow
              </small>
            </div>
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
