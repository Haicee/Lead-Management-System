<?php
/**
 * Migration: Add Performance Indexes for Masterlist Lead Queries
 */

return new class {
    public function up(PDO $db): void
    {
        $this->addIndexIfNotExists($db, 'leads', 'idx_leads_sic_desc', '(`sic_description`(50))');
        $this->addIndexIfNotExists($db, 'leads', 'idx_leads_annual_sales', '(`annual_sales`(30))');
        $this->addIndexIfNotExists($db, 'leads', 'idx_leads_contact_name', '(`contact_name`(50))');
        $this->addIndexIfNotExists($db, 'leads', 'idx_leads_cell_number', '(`cell_number`)');
    }

    public function down(PDO $db): void
    {
        $this->dropIndexIfExists($db, 'leads', 'idx_leads_sic_desc');
        $this->dropIndexIfExists($db, 'leads', 'idx_leads_annual_sales');
        $this->dropIndexIfExists($db, 'leads', 'idx_leads_contact_name');
        $this->dropIndexIfExists($db, 'leads', 'idx_leads_cell_number');
    }

    private function addIndexIfNotExists(PDO $db, string $table, string $indexName, string $columns): void
    {
        $stmt = $db->prepare("SHOW INDEX FROM `{$table}` WHERE `Key_name` = ?");
        $stmt->execute([$indexName]);
        if (!$stmt->fetch()) {
            $db->exec("ALTER TABLE `{$table}` ADD INDEX `{$indexName}` {$columns}");
        }
    }

    private function dropIndexIfExists(PDO $db, string $table, string $indexName): void
    {
        $stmt = $db->prepare("SHOW INDEX FROM `{$table}` WHERE `Key_name` = ?");
        $stmt->execute([$indexName]);
        if ($stmt->fetch()) {
            $db->exec("ALTER TABLE `{$table}` DROP INDEX `{$indexName}`");
        }
    }
};

