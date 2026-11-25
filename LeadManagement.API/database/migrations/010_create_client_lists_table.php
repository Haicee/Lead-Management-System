<?php
/**
 * Migration: Create Client Lists Table
 *
 * Tracks each batch/list of leads assigned to a client (e.g., List 01, List 02, ...)
 * so admins can view, export, and delete individual assigned lists per client.
 */

return new class {
    public function up(PDO $db): void
    {
        // 1. Create client_lists table
        $db->exec("
            CREATE TABLE IF NOT EXISTS `client_lists` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `client_id` INT UNSIGNED NOT NULL,
                `list_name` VARCHAR(100) NOT NULL,
                `created_at` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

                INDEX `idx_clists_client_id` (`client_id`),
                CONSTRAINT `fk_clists_client` FOREIGN KEY (`client_id`)
                    REFERENCES `clients` (`id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
        ");

        // 2. Add client_list_id column to leads table if not present
        $stmtCol = $db->prepare("
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'leads'
              AND COLUMN_NAME = 'client_list_id'
        ");
        $stmtCol->execute();
        if (((int)$stmtCol->fetchColumn()) === 0) {
            $db->exec("ALTER TABLE `leads` ADD COLUMN `client_list_id` INT UNSIGNED NULL DEFAULT NULL AFTER `client_id`");
            $db->exec("ALTER TABLE `leads` ADD INDEX `idx_leads_client_list_id` (`client_list_id`)");
            try {
                $db->exec("ALTER TABLE `leads` ADD CONSTRAINT `fk_leads_client_list` FOREIGN KEY (`client_list_id`) REFERENCES `client_lists` (`id`) ON DELETE SET NULL");
            } catch (Exception $e) {
                // Ignore if constraint already exists
            }
        }

        // 3. Backfill any existing client-assigned leads without a client_list_id into 'List 01'
        $stmtClients = $db->query("
            SELECT DISTINCT `client_id`
            FROM `leads`
            WHERE `client_id` IS NOT NULL AND `client_id` > 0 AND `client_list_id` IS NULL
        ");
        $clientIds = $stmtClients->fetchAll(PDO::FETCH_COLUMN);

        foreach ($clientIds as $cid) {
            $cid = (int)$cid;
            $ins = $db->prepare("INSERT INTO `client_lists` (`client_id`, `list_name`) VALUES (?, 'List 01')");
            $ins->execute([$cid]);
            $listId = (int)$db->lastInsertId();

            $upd = $db->prepare("UPDATE `leads` SET `client_list_id` = ? WHERE `client_id` = ? AND `client_list_id` IS NULL");
            $upd->execute([$listId, $cid]);
        }
    }

    public function down(PDO $db): void
    {
        try { $db->exec("ALTER TABLE `leads` DROP FOREIGN KEY `fk_leads_client_list`"); } catch (Exception $e) {}
        try { $db->exec("ALTER TABLE `leads` DROP COLUMN `client_list_id`"); } catch (Exception $e) {}
        $db->exec("DROP TABLE IF EXISTS `client_lists`");
    }
};
