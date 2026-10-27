<?php
/**
 * Migration: Create Users Table
 * 
 * Stores user accounts with role-based access control.
 * Roles: admin, user
 */

return new class {
    public function up(PDO $db): void
    {
        $db->exec("
            CREATE TABLE `users` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `username` VARCHAR(100) NOT NULL UNIQUE,
                `password` VARCHAR(255) NOT NULL COMMENT 'bcrypt hashed password',
                `full_name` VARCHAR(150) NOT NULL DEFAULT '',
                `email` VARCHAR(200) DEFAULT NULL,
                `role` ENUM('admin', 'user') NOT NULL DEFAULT 'user',
                `is_active` TINYINT(1) NOT NULL DEFAULT 1,
                `last_login_at` TIMESTAMP NULL DEFAULT NULL,
                `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                
                INDEX `idx_users_role` (`role`),
                INDEX `idx_users_is_active` (`is_active`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
        ");
    }

    public function down(PDO $db): void
    {
        $db->exec("DROP TABLE IF EXISTS `users`");
    }
};
