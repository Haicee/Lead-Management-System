<?php
/**
 * Migration: Create Client Leads Table
 * 
 * Separates client uploaded file data from the main masterlist leads table into its own dedicated table.
 */

return new class {
    public function up(PDO $db): void
    {
        // 1. Create client_leads table
        $db->exec("
            CREATE TABLE IF NOT EXISTS `client_leads` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `client_id` INT UNSIGNED NOT NULL,
                `file_id` INT UNSIGNED NOT NULL,
                `call_date` VARCHAR(50) NULL DEFAULT NULL,
                `call_disposition` VARCHAR(100) NOT NULL DEFAULT '',
                `assigned_agent_id` INT UNSIGNED NULL DEFAULT NULL,
                `assigned_agent` VARCHAR(150) NOT NULL DEFAULT '',
                `notes` TEXT NULL,
                `call_count` INT UNSIGNED NOT NULL DEFAULT 1,
                `status` VARCHAR(50) NOT NULL DEFAULT 'New Lead',
                `business_name` VARCHAR(255) NOT NULL DEFAULT '',
                `address` VARCHAR(255) DEFAULT '',
                `city` VARCHAR(100) DEFAULT '',
                `county` VARCHAR(100) DEFAULT '',
                `state` VARCHAR(20) DEFAULT 'NE',
                `zip_code` VARCHAR(30) DEFAULT '',
                `cell_number` VARCHAR(50) DEFAULT '',
                `fax` VARCHAR(50) DEFAULT '',
                `email` VARCHAR(255) DEFAULT '',
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

                INDEX `idx_cl_client_id` (`client_id`),
                INDEX `idx_cl_file_id` (`file_id`),
                INDEX `idx_cl_disp` (`call_disposition`),
                INDEX `idx_cl_agent` (`assigned_agent_id`),
                CONSTRAINT `fk_cl_client` FOREIGN KEY (`client_id`) 
                    REFERENCES `clients` (`id`) ON DELETE CASCADE,
                CONSTRAINT `fk_cl_file` FOREIGN KEY (`file_id`) 
                    REFERENCES `client_files` (`id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
        ");

        // 2. Migrate existing leads that belonged to client_files into client_leads
        $db->exec("
            INSERT INTO `client_leads` (
                client_id, file_id, call_date, call_disposition, assigned_agent_id, assigned_agent,
                notes, call_count, status, business_name, address, city, county, state, zip_code,
                cell_number, fax, email, annual_sales, number_of_employees, title, contact_name,
                website, sic_code, sic_description, naics_code, naics_description, created_at
            )
            SELECT 
                cf.client_id, cf.id, COALESCE(l.call_date, DATE_FORMAT(l.created_at, '%m/%d/%y')),
                COALESCE(l.call_disposition, ''), l.assigned_agent_id, COALESCE(l.assigned_agent, ''),
                l.notes, COALESCE(l.call_count, 1), l.status, l.business_name, l.address, l.city,
                l.county, l.state, l.zip_code, l.cell_number, l.fax, '', l.annual_sales,
                l.number_of_employees, l.title, l.contact_name, l.website, l.sic_code,
                l.sic_description, l.naics_code, l.naics_description, l.created_at
            FROM `leads` l
            INNER JOIN `client_files` cf ON l.batch_id = cf.batch_id
        ");

        // 3. Remove client uploaded leads from main leads table and batch history so they don't pollute the masterlist
        $db->exec("
            DELETE l FROM `leads` l
            INNER JOIN `client_files` cf ON l.batch_id = cf.batch_id
        ");
        $db->exec("
            DELETE b FROM `import_batches` b
            INNER JOIN `client_files` cf ON b.id = cf.batch_id
        ");
    }

    public function down(PDO $db): void
    {
        $db->exec("DROP TABLE IF EXISTS `client_leads`");
    }
};

