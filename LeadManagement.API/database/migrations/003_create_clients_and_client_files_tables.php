<?php
/**
 * Migration: Create Clients, Client Files, and Client Agent Assignments Tables
 */

return new class {
    public function up(PDO $db): void
    {
        // 1. Clients Table (First Name and Last Name only as specified)
        $db->exec("
            CREATE TABLE IF NOT EXISTS `clients` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `first_name` VARCHAR(100) NOT NULL,
                `last_name` VARCHAR(100) NOT NULL,
                `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                
                INDEX `idx_clients_name` (`last_name`, `first_name`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
        ");

        // 2. Client Files Table (Stores files imported under a specific client)
        $db->exec("
            CREATE TABLE IF NOT EXISTS `client_files` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `client_id` INT UNSIGNED NOT NULL,
                `client_name` VARCHAR(200) NOT NULL DEFAULT '',
                `file_name` VARCHAR(255) NOT NULL,
                `file_size` VARCHAR(50) NOT NULL DEFAULT '',
                `total_leads` INT UNSIGNED NOT NULL DEFAULT 0,
                `batch_id` INT UNSIGNED NULL,
                `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                
                INDEX `idx_cf_client_id` (`client_id`),
                CONSTRAINT `fk_cf_client` FOREIGN KEY (`client_id`) 
                    REFERENCES `clients` (`id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
        ");

        // 3. Client Agent Assignments (Checkbox assignments linking agents [role = 'user'] to clients)
        $db->exec("
            CREATE TABLE IF NOT EXISTS `client_agents` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `client_id` INT UNSIGNED NOT NULL,
                `user_id` INT UNSIGNED NOT NULL,
                `assigned_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                
                UNIQUE KEY `uq_client_user` (`client_id`, `user_id`),
                INDEX `idx_ca_client` (`client_id`),
                INDEX `idx_ca_user` (`user_id`),
                CONSTRAINT `fk_ca_client` FOREIGN KEY (`client_id`) 
                    REFERENCES `clients` (`id`) ON DELETE CASCADE,
                CONSTRAINT `fk_ca_user` FOREIGN KEY (`user_id`) 
                    REFERENCES `users` (`id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
        ");
    }

    public function down(PDO $db): void
    {
        $db->exec("DROP TABLE IF EXISTS `client_agents`");
        $db->exec("DROP TABLE IF EXISTS `client_files`");
        $db->exec("DROP TABLE IF EXISTS `clients`");
    }
};

