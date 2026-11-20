<?php
/**
 * Migration: Centralize Leads Masterlist
 * 
 * 1. Adds client_id, call_date, call_disposition, assigned_agent_id, assigned_agent,
 *    notes, call_count, and email columns to the master `leads` table.
 * 2. Adds foreign keys and indexes.
 * 3. Migrates existing records from `client_leads` into `leads` so all client data
 *    is unified in one single centralized masterlist table.
 */

return new class {
    public function up(PDO $db): void
    {
        // 1. Helper function to check if a column exists
        $hasColumn = function(PDO $db, string $table, string $column): bool {
            $stmt = $db->prepare("
                SELECT COUNT(*) 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_SCHEMA = DATABASE() 
                  AND TABLE_NAME = ? 
                  AND COLUMN_NAME = ?
            ");
            $stmt->execute([$table, $column]);
            return ((int)$stmt->fetchColumn()) > 0;
        };

        // Add client_id if not present
        if (!$hasColumn($db, 'leads', 'client_id')) {
            $db->exec("ALTER TABLE `leads` ADD COLUMN `client_id` INT UNSIGNED NULL DEFAULT NULL AFTER `batch_name`");
            $db->exec("ALTER TABLE `leads` ADD INDEX `idx_leads_client_id` (`client_id`)");
            try {
                $db->exec("ALTER TABLE `leads` ADD CONSTRAINT `fk_leads_client` FOREIGN KEY (`client_id`) REFERENCES `clients` (`id`) ON DELETE SET NULL");
            } catch (Exception $e) {
                // Ignore if constraint exists
            }
        }

        // Add call_date if not present
        if (!$hasColumn($db, 'leads', 'call_date')) {
            $db->exec("ALTER TABLE `leads` ADD COLUMN `call_date` VARCHAR(50) NULL DEFAULT NULL AFTER `client_id`");
        }

        // Add call_disposition if not present
        if (!$hasColumn($db, 'leads', 'call_disposition')) {
            $db->exec("ALTER TABLE `leads` ADD COLUMN `call_disposition` VARCHAR(100) NOT NULL DEFAULT '' AFTER `call_date`");
            $db->exec("ALTER TABLE `leads` ADD INDEX `idx_leads_disp` (`call_disposition`)");
        }

        // Add assigned_agent_id if not present
        if (!$hasColumn($db, 'leads', 'assigned_agent_id')) {
            $db->exec("ALTER TABLE `leads` ADD COLUMN `assigned_agent_id` INT UNSIGNED NULL DEFAULT NULL AFTER `call_disposition`");
            $db->exec("ALTER TABLE `leads` ADD INDEX `idx_leads_agent` (`assigned_agent_id`)");
            try {
                $db->exec("ALTER TABLE `leads` ADD CONSTRAINT `fk_leads_agent` FOREIGN KEY (`assigned_agent_id`) REFERENCES `users` (`id`) ON DELETE SET NULL");
            } catch (Exception $e) {
                // Ignore if constraint exists
            }
        }

        // Add assigned_agent if not present
        if (!$hasColumn($db, 'leads', 'assigned_agent')) {
            $db->exec("ALTER TABLE `leads` ADD COLUMN `assigned_agent` VARCHAR(150) NOT NULL DEFAULT '' AFTER `assigned_agent_id`");
        }

        // Add notes if not present
        if (!$hasColumn($db, 'leads', 'notes')) {
            $db->exec("ALTER TABLE `leads` ADD COLUMN `notes` TEXT NULL AFTER `assigned_agent`");
        }

        // Add call_count if not present
        if (!$hasColumn($db, 'leads', 'call_count')) {
            $db->exec("ALTER TABLE `leads` ADD COLUMN `call_count` INT UNSIGNED NOT NULL DEFAULT 1 AFTER `notes`");
        }

        // Add email if not present
        if (!$hasColumn($db, 'leads', 'email')) {
            $db->exec("ALTER TABLE `leads` ADD COLUMN `email` VARCHAR(255) DEFAULT '' AFTER `cell_number`");
        }

        // 2. Migrate existing client_leads data into leads table if client_leads exists
        $stmtTable = $db->query("
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_SCHEMA = DATABASE() 
              AND TABLE_NAME = 'client_leads'
        ");
        if (((int)$stmtTable->fetchColumn()) > 0) {
            $countClientLeads = (int)$db->query("SELECT COUNT(*) FROM `client_leads`")->fetchColumn();
            if ($countClientLeads > 0) {
                $db->exec("
                    INSERT INTO `leads` (
                        client_id, batch_id, batch_name, call_date, call_disposition,
                        assigned_agent_id, assigned_agent, notes, call_count, status,
                        business_name, address, city, county, state, zip_code, cell_number,
                        email, fax, annual_sales, number_of_employees, title, contact_name,
                        website, sic_code, sic_description, naics_code, naics_description, created_at
                    )
                    SELECT 
                        cl.client_id,
                        NULL,
                        CONCAT('Client Assign: ', COALESCE(CONCAT(c.first_name, ' ', c.last_name), CONCAT('Client #', cl.client_id))),
                        cl.call_date,
                        COALESCE(cl.call_disposition, ''),
                        cl.assigned_agent_id,
                        COALESCE(cl.assigned_agent, ''),
                        cl.notes,
                        COALESCE(cl.call_count, 1),
                        cl.status,
                        cl.business_name,
                        cl.address,
                        cl.city,
                        cl.county,
                        cl.state,
                        cl.zip_code,
                        cl.cell_number,
                        cl.email,
                        cl.fax,
                        cl.annual_sales,
                        cl.number_of_employees,
                        cl.title,
                        cl.contact_name,
                        cl.website,
                        cl.sic_code,
                        cl.sic_description,
                        cl.naics_code,
                        cl.naics_description,
                        cl.created_at
                    FROM `client_leads` cl
                    LEFT JOIN `clients` c ON cl.client_id = c.id
                ");
            }
        }
    }

    public function down(PDO $db): void
    {
        try { $db->exec("ALTER TABLE `leads` DROP FOREIGN KEY `fk_leads_client`"); } catch (Exception $e) {}
        try { $db->exec("ALTER TABLE `leads` DROP FOREIGN KEY `fk_leads_agent`"); } catch (Exception $e) {}
        try { $db->exec("ALTER TABLE `leads` DROP COLUMN `client_id`"); } catch (Exception $e) {}
        try { $db->exec("ALTER TABLE `leads` DROP COLUMN `call_date`"); } catch (Exception $e) {}
        try { $db->exec("ALTER TABLE `leads` DROP COLUMN `call_disposition`"); } catch (Exception $e) {}
        try { $db->exec("ALTER TABLE `leads` DROP COLUMN `assigned_agent_id`"); } catch (Exception $e) {}
        try { $db->exec("ALTER TABLE `leads` DROP COLUMN `assigned_agent`"); } catch (Exception $e) {}
        try { $db->exec("ALTER TABLE `leads` DROP COLUMN `notes`"); } catch (Exception $e) {}
        try { $db->exec("ALTER TABLE `leads` DROP COLUMN `call_count`"); } catch (Exception $e) {}
        try { $db->exec("ALTER TABLE `leads` DROP COLUMN `email`"); } catch (Exception $e) {}
    }
};
