<?php
/**
 * Seeder: Admin User
 * 
 * Creates the default admin account with bcrypt-hashed password.
 * 
 * Credentials:
 *   Username: Admin
 *   Password: Admin123
 *   Role:     admin
 */

return new class {
    public function run(PDO $db): void
    {
        // Check if admin already exists to prevent duplicates
        $stmt = $db->prepare("SELECT COUNT(*) FROM users WHERE username = ?");
        $stmt->execute(['Admin']);
        
        if ((int) $stmt->fetchColumn() > 0) {
            echo "  ⏭ Admin user already exists, skipping.\n";
            return;
        }

        // Hash the password using bcrypt (PHP's default secure algorithm)
        $hashedPassword = password_hash('Admin123', PASSWORD_BCRYPT);

        $stmt = $db->prepare("
            INSERT INTO users (username, password, full_name, email, role, is_active)
            VALUES (?, ?, ?, ?, ?, ?)
        ");

        $stmt->execute([
            'Admin',
            $hashedPassword,
            'System Administrator',
            'admin@lms.local',
            'admin',
            1,
        ]);

        echo "  ✓ Admin user created (username: Admin, password: Admin123 [bcrypt encrypted])\n";
    }
};
