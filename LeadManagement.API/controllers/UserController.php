<?php
/**
 * User Controller
 * 
 * Handles CRUD operations for user accounts.
 */

require_once __DIR__ . '/../core/ActivityLogger.php';

class UserController
{
    private PDO $db;

    public function __construct()
    {
        $this->db = Database::getConnection();
    }

    /**
     * GET /users
     * 
     * Returns all users.
     */
    public function index(): void
    {
        $stmt = $this->db->query("
            SELECT id, username, full_name, email, role, is_active, last_login_at, created_at, updated_at 
            FROM users 
            ORDER BY created_at DESC
        ");

        Response::success($stmt->fetchAll(), 'Users retrieved successfully');
    }

    /**
     * POST /users
     * 
     * Creates a new user account.
     * Body: { "username": "...", "password": "...", "full_name": "...", "email": "...", "role": "user" }
     */
    public function store(array $input): void
    {
        $username = trim($input['username'] ?? '');
        $password = $input['password'] ?? '';
        $fullName = trim($input['full_name'] ?? '');
        $email    = trim($input['email'] ?? '');
        $role     = $input['role'] ?? 'user';

        // Validation
        $errors = [];
        if (empty($username))               $errors[] = 'Username is required.';
        if (strlen($username) < 3)           $errors[] = 'Username must be at least 3 characters.';
        if (empty($password))                $errors[] = 'Password is required.';
        if (strlen($password) < 6)           $errors[] = 'Password must be at least 6 characters.';
        if (!in_array($role, ['admin', 'user'])) $errors[] = 'Role must be either admin or user.';

        if (!empty($errors)) {
            Response::error('Validation failed.', 422, $errors);
        }

        // Check for duplicate username
        $stmt = $this->db->prepare("SELECT COUNT(*) FROM users WHERE username = ?");
        $stmt->execute([$username]);
        if ((int) $stmt->fetchColumn() > 0) {
            Response::error('Username already exists.', 409);
        }

        // Hash password and insert
        $hashedPassword = password_hash($password, PASSWORD_BCRYPT);

        $stmt = $this->db->prepare("
            INSERT INTO users (username, password, full_name, email, role, is_active) 
            VALUES (?, ?, ?, ?, ?, 1)
        ");
        $stmt->execute([$username, $hashedPassword, $fullName, $email, $role]);

        $newId = (int) $this->db->lastInsertId();

        $stmt = $this->db->prepare("
            SELECT id, username, full_name, email, role, is_active, last_login_at, created_at, updated_at 
            FROM users WHERE id = ?
        ");
        $stmt->execute([$newId]);

        ActivityLogger::log(
            'USER_MANAGE',
            'Created User Account',
            "Created user account '{$username}' (Role: " . ucfirst($role) . ")"
        );

        Response::success($stmt->fetch(), 'User created successfully', 201);
    }

    /**
     * PUT/POST /users/{id} or POST /users/update
     * 
     * Updates an existing user account.
     * Body: { "username": "...", "full_name": "...", "email": "...", "role": "user", "is_active": 1, "password": "..." (optional) }
     */
    public function update(int $id, array $input): void
    {
        if ($id <= 0) {
            Response::error('Invalid user ID.', 400);
        }

        // Find existing user
        $stmt = $this->db->prepare("SELECT * FROM users WHERE id = ?");
        $stmt->execute([$id]);
        $existing = $stmt->fetch();

        if (!$existing) {
            Response::error('User account not found.', 404);
        }

        $username = trim($input['username'] ?? $existing['username']);
        $fullName = trim($input['full_name'] ?? $existing['full_name']);
        $email    = trim($input['email'] ?? $existing['email']);
        $role     = strtolower(trim($input['role'] ?? $existing['role']));
        $isActive = isset($input['is_active']) ? (int) $input['is_active'] : (int) $existing['is_active'];
        $password = $input['password'] ?? '';

        // Validation
        $errors = [];
        if (empty($username)) {
            $errors[] = 'Username is required.';
        } elseif (strlen($username) < 3) {
            $errors[] = 'Username must be at least 3 characters.';
        }

        if (empty($fullName)) {
            $errors[] = 'Full name is required.';
        }

        if (!in_array($role, ['admin', 'user'])) {
            $errors[] = 'Role must be either admin or user.';
        }

        if (!in_array($isActive, [0, 1])) {
            $isActive = 1;
        }

        // Password validation (only if provided)
        if (!empty($password) && strlen($password) < 6) {
            $errors[] = 'New password must be at least 6 characters.';
        }

        if (!empty($errors)) {
            Response::error('Validation failed.', 422, $errors);
        }

        // Check duplicate username for another user
        $stmt = $this->db->prepare("SELECT COUNT(*) FROM users WHERE username = ? AND id != ?");
        $stmt->execute([$username, $id]);
        if ((int) $stmt->fetchColumn() > 0) {
            Response::error("Username '{$username}' is already taken by another account.", 409);
        }

        // Guard against locking out the system: prevent demoting or deactivating the last active admin
        if ($existing['role'] === 'admin' && ($role !== 'admin' || $isActive === 0)) {
            $stmt = $this->db->query("SELECT COUNT(*) FROM users WHERE role = 'admin' AND is_active = 1");
            $adminCount = (int) $stmt->fetchColumn();
            if ($adminCount <= 1) {
                Response::error('Cannot deactivate or demote the only active Administrator in the system.', 400);
            }
        }

        // Update database
        if (!empty($password)) {
            $hashedPassword = password_hash($password, PASSWORD_BCRYPT);
            $stmt = $this->db->prepare("
                UPDATE users 
                SET username = ?, full_name = ?, email = ?, role = ?, is_active = ?, password = ?
                WHERE id = ?
            ");
            $stmt->execute([$username, $fullName, $email, $role, $isActive, $hashedPassword, $id]);
        } else {
            $stmt = $this->db->prepare("
                UPDATE users 
                SET username = ?, full_name = ?, email = ?, role = ?, is_active = ?
                WHERE id = ?
            ");
            $stmt->execute([$username, $fullName, $email, $role, $isActive, $id]);
        }

        // Fetch updated user
        $stmt = $this->db->prepare("
            SELECT id, username, full_name, email, role, is_active, last_login_at, created_at, updated_at 
            FROM users WHERE id = ?
        ");
        $stmt->execute([$id]);

        ActivityLogger::log(
            'USER_MANAGE',
            'Updated User Account',
            "Updated user account '{$username}' (Role: " . ucfirst($role) . ")"
        );

        Response::success($stmt->fetch(), 'User account updated successfully');
    }

    /**
     * DELETE /users/{id} or POST /users/delete
     * 
     * Deletes a user account.
     */
    public function destroy(int $id): void
    {
        if ($id <= 0) {
            Response::error('Invalid user ID.', 400);
        }

        // Find user
        $stmt = $this->db->prepare("SELECT id, username, role FROM users WHERE id = ?");
        $stmt->execute([$id]);
        $user = $stmt->fetch();

        if (!$user) {
            Response::error('User account not found.', 404);
        }

        // Prevent deleting the only administrator
        if ($user['role'] === 'admin') {
            $stmt = $this->db->query("SELECT COUNT(*) FROM users WHERE role = 'admin'");
            $adminCount = (int) $stmt->fetchColumn();
            if ($adminCount <= 1) {
                Response::error('Cannot delete the only Administrator account in the system.', 400);
            }
        }

        // Clean up assigned agent from leads if any
        $stmt = $this->db->prepare("UPDATE client_leads SET assigned_agent_id = NULL WHERE assigned_agent_id = ?");
        $stmt->execute([$id]);

        // Delete user (Foreign keys like client_assignments will cascade delete)
        $stmt = $this->db->prepare("DELETE FROM users WHERE id = ?");
        $stmt->execute([$id]);

        ActivityLogger::log(
            'USER_MANAGE',
            'Deleted User Account',
            "Deleted user account '{$user['username']}'"
        );

        Response::success([
            'deleted_id' => $id,
            'username'   => $user['username']
        ], "User '{$user['username']}' deleted successfully.");
    }
}
