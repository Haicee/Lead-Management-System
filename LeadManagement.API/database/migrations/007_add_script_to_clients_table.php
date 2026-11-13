<?php
/**
 * Migration: Add script fields to clients table
 */

return new class {
    public function up(PDO $db): void
    {
        // Add script fields to `clients` table if not exists
        $columns = [
            "`script_name` VARCHAR(255) NULL DEFAULT NULL AFTER `last_name`",
            "`script_size` VARCHAR(50) NULL DEFAULT NULL AFTER `script_name`",
            "`script_type` VARCHAR(50) NULL DEFAULT NULL AFTER `script_size`",
            "`script_content` MEDIUMTEXT NULL DEFAULT NULL AFTER `script_type`",
            "`script_updated_at` TIMESTAMP NULL DEFAULT NULL AFTER `script_content`"
        ];

        foreach ($columns as $colDef) {
            $colName = explode(' ', trim($colDef))[0];
            $colName = trim($colName, '`');
            
            // Check if column already exists
            $check = $db->query("SHOW COLUMNS FROM `clients` LIKE '{$colName}'");
            if ($check->rowCount() === 0) {
                $db->exec("ALTER TABLE `clients` ADD COLUMN {$colDef}");
            }
        }
    }

    public function down(PDO $db): void
    {
        $cols = ['script_name', 'script_size', 'script_type', 'script_content', 'script_updated_at'];
        foreach ($cols as $col) {
            $check = $db->query("SHOW COLUMNS FROM `clients` LIKE '{$col}'");
            if ($check->rowCount() > 0) {
                $db->exec("ALTER TABLE `clients` DROP COLUMN `{$col}`");
            }
        }
    }
};

