<?php
/**
 * Migration: Multi-Client Lead Assignments & Lead Call History
 *
 * 1. Creates `lead_client_assignments` junction table so a single lead can be
 *    assigned to multiple clients simultaneously.
 * 2. Backfills existing `leads.client_id` / `leads.client_list_id` assignments.
 * 3. Creates `lead_call_history` table to track every call made on a lead,
 *    recording which agent called, which clients were assigned, disposition, and notes.
 */

return new class {
    public function up(PDO $db): void
    {
        // 1. Junction table for multi-client lead assignments
        $db->exec("
            CREATE TABLE IF NOT EXISTS `lead_client_assignments` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `lead_id` INT UNSIGNED NOT NULL,
                `client_id` INT UNSIGNED NOT NULL,
                `client_list_id` INT UNSIGNED NULL DEFAULT NULL,
                `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

                UNIQUE KEY `uq_lca_lead_client` (`lead_id`, `client_id`),
                INDEX `idx_lca_client_id` (`client_id`),
                INDEX `idx_lca_list_id` (`client_list_id`),
                CONSTRAINT `fk_lca_lead` FOREIGN KEY (`lead_id`)
                    REFERENCES `leads` (`id`) ON DELETE CASCADE,
                CONSTRAINT `fk_lca_client` FOREIGN KEY (`client_id`)
                    REFERENCES `clients` (`id`) ON DELETE CASCADE,
                CONSTRAINT `fk_lca_list` FOREIGN KEY (`client_list_id`)
                    REFERENCES `client_lists` (`id`) ON DELETE SET NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
        ");

        // 2. Backfill existing single-client assignments from `leads`
        $db->exec("
            INSERT IGNORE INTO `lead_client_assignments` (`lead_id`, `client_id`, `client_list_id`)
            SELECT `id`, `client_id`, `client_list_id`
            FROM `leads`
            WHERE `client_id` IS NOT NULL AND `client_id` > 0
        ");

        // 3. Lead call history table
        $db->exec("
            CREATE TABLE IF NOT EXISTS `lead_call_history` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `lead_id` INT UNSIGNED NOT NULL,
                `agent_id` INT UNSIGNED NULL DEFAULT NULL,
                `agent_name` VARCHAR(150) NOT NULL DEFAULT '',
                `client_names` TEXT NULL,
                `call_disposition` VARCHAR(100) NOT NULL DEFAULT '',
                `notes` TEXT NULL,
                `call_number` INT UNSIGNED NOT NULL DEFAULT 1,
                `called_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

                INDEX `idx_lch_lead_id` (`lead_id`),
                INDEX `idx_lch_called_at` (`called_at`),
                CONSTRAINT `fk_lch_lead` FOREIGN KEY (`lead_id`)
                    REFERENCES `leads` (`id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
        ");
    }

    public function down(PDO $db): void
    {
        $db->exec("DROP TABLE IF EXISTS `lead_call_history`");
        $db->exec("DROP TABLE IF EXISTS `lead_client_assignments`");
    }
};
