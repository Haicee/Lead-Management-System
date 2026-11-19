<?php
/**
 * Migration: Create Admin Activities Table
 * 
 * Tracks the latest 10 activities performed by administrators.
 * Supports:
 *  - imported list, date, how many
 *  - export list, date, how many
 *  - deleted leads, date and how many
 *  - managing client (who add, upload, delete)
 *  - user account (who add, edit, delete)
 */

return new class {
    public function up(PDO $db): void
    {
        $db->exec("
            CREATE TABLE IF NOT EXISTS `admin_activities` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `admin_id` INT UNSIGNED NULL DEFAULT NULL,
                `admin_name` VARCHAR(150) NOT NULL DEFAULT 'Administrator',
                `action_type` VARCHAR(50) NOT NULL, -- IMPORT, EXPORT, DELETE_LEADS, CLIENT_MANAGE, USER_MANAGE
                `title` VARCHAR(255) NOT NULL,
                `description` TEXT NOT NULL,
                `item_count` INT NULL DEFAULT NULL,
                `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX `idx_act_type` (`action_type`),
                INDEX `idx_act_created` (`created_at`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");
    }

    public function down(PDO $db): void
    {
        $db->exec("DROP TABLE IF EXISTS `admin_activities`;");
    }
};
