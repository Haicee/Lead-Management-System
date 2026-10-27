<?php
/**
 * Authentication Controller
 * 
 * Handles user login and session/token management.
 * Passwords are verified using PHP's password_verify() against bcrypt hashes.
 */

class AuthController
{
    private PDO $db;

    public function __construct()
    {
        $this->db = Database::getConnection();
    }

    /**
     * POST /auth/login
     * 
     * Authenticates a user with username and password.
     * Returns user data and a simple token on success.
     * 
     * Body: { "username": "Admin", "password": "Admin123" }
     */
    public function login(array $input): void
    {
        // Validate required fields
        $username = trim($input['username'] ?? '');
        $password = $input['password'] ?? '';

        if (empty($username) || empty($password)) {
            Response::error('Username and password are required.', 422);
        }

        // Look up user
        $stmt = $this->db->prepare("
            SELECT id, username, password, full_name, email, role, is_active 
            FROM users 
            WHERE username = ?
            LIMIT 1
        ");
        $stmt->execute([$username]);
        $user = $stmt->fetch();

        if (!$user) {
            Response::error('Invalid username or password.', 401);
        }

        // Check if account is active
        if (!$user['is_active']) {
            Response::error('This account has been deactivated. Contact your administrator.', 403);
        }

        // Verify password against bcrypt hash
        if (!password_verify($password, $user['password'])) {
            Response::error('Invalid username or password.', 401);
        }

        // Update last login timestamp
        $updateStmt = $this->db->prepare("UPDATE users SET last_login_at = NOW() WHERE id = ?");
        $updateStmt->execute([$user['id']]);

        // Generate a simple session token (for desktop app use)
        $token = bin2hex(random_bytes(32));

        // Return user data (never return the password hash)
        Response::success([
            'token' => $token,
            'user'  => [
                'id'        => (int) $user['id'],
                'username'  => $user['username'],
                'full_name' => $user['full_name'],
                'email'     => $user['email'],
                'role'      => $user['role'],
            ],
        ], 'Login successful');
    }

    /**
     * GET /auth/me
     * 
     * Returns the currently authenticated user's info.
     * (Placeholder — will be implemented with proper token validation later)
     */
    public function me(): void
    {
        // TODO: Implement token-based auth validation
        Response::error('Token authentication not yet implemented.', 501);
    }
}
