<?php
/**
 * Migration: Create Import Batches and Leads Tables
 * 
 * Stores imported lead batches and all lead records with 18 business data columns.
 */

return new class {
    public function up(PDO $db): void
    {
        // 1. Import Batches Table (Audit trail of every imported file)
        $db->exec("
            CREATE TABLE IF NOT EXISTS `import_batches` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `batch_name` VARCHAR(150) NOT NULL,
                `file_name` VARCHAR(255) NOT NULL,
                `total_leads` INT UNSIGNED NOT NULL DEFAULT 0,
                `initial_status` VARCHAR(50) NOT NULL DEFAULT 'New Lead',
                `imported_by` INT UNSIGNED NULL,
                `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                
                INDEX `idx_batches_created` (`created_at`),
                CONSTRAINT `fk_batches_user` FOREIGN KEY (`imported_by`) 
                    REFERENCES `users` (`id`) ON DELETE SET NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
        ");

        // 2. Leads Table (All leads matching the exact table column headers)
        $db->exec("
            CREATE TABLE IF NOT EXISTS `leads` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `batch_id` INT UNSIGNED NULL,
                `batch_name` VARCHAR(150) NOT NULL DEFAULT '',
                `status` VARCHAR(50) NOT NULL DEFAULT 'New Lead',
                `business_name` VARCHAR(255) NOT NULL DEFAULT '',
                `address` VARCHAR(255) DEFAULT '',
                `city` VARCHAR(100) DEFAULT '',
                `county` VARCHAR(100) DEFAULT '',
                `state` VARCHAR(20) DEFAULT 'NE',
                `zip_code` VARCHAR(30) DEFAULT '',
                `cell_number` VARCHAR(50) DEFAULT '',
                `fax` VARCHAR(50) DEFAULT '',
                `annual_sales` VARCHAR(100) DEFAULT '',
                `number_of_employees` VARCHAR(100) DEFAULT '',
                `title` VARCHAR(150) DEFAULT '',
                `contact_name` VARCHAR(200) DEFAULT '',
                `website` VARCHAR(255) DEFAULT '',
                `sic_code` VARCHAR(50) DEFAULT '',
                `sic_description` VARCHAR(255) DEFAULT '',
                `naics_code` VARCHAR(50) DEFAULT '',
                `naics_description` VARCHAR(255) DEFAULT '',
                `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                
                INDEX `idx_leads_status` (`status`),
                INDEX `idx_leads_business_name` (`business_name`(50)),
                INDEX `idx_leads_city` (`city`),
                INDEX `idx_leads_batch_id` (`batch_id`),
                CONSTRAINT `fk_leads_batch` FOREIGN KEY (`batch_id`) 
                    REFERENCES `import_batches` (`id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
        ");
    }

    public function down(PDO $db): void
    {
        $db->exec("DROP TABLE IF EXISTS `leads`");
        $db->exec("DROP TABLE IF EXISTS `import_batches`");
    }
};
