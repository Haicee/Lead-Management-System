<?php
/**
 * User Controller
 * 
 * Handles CRUD operations for user accounts.
 */

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
     * Returns all users (admin only — authorization to be added later).
     */
    public function index(): void
    {
        $stmt = $this->db->query("
            SELECT id, username, full_name, email, role, is_active, last_login_at, created_at 
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
            INSERT INTO users (username, password, full_name, email, role) 
            VALUES (?, ?, ?, ?, ?)
        ");
        $stmt->execute([$username, $hashedPassword, $fullName, $email, $role]);

        $newId = (int) $this->db->lastInsertId();

        Response::success([
            'id'        => $newId,
            'username'  => $username,
            'full_name' => $fullName,
            'email'     => $email,
            'role'      => $role,
        ], 'User created successfully', 201);
    }
}
