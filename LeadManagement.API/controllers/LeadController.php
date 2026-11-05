<?php
/**
 * Lead Controller
 * 
 * Handles batch lead imports, querying leads, and import audit history.
 */

class LeadController
{
    private PDO $db;

    public function __construct()
    {
        $this->db = Database::getConnection();
    }

    /**
     * POST /leads/import
     * 
     * Stores an imported batch and all parsed lead records in the database.
     * Uses a PDO transaction to guarantee atomic persistence.
     * 
     * Expected JSON Payload:
     * {
     *   "batch_name": "Nebraska_October_2026",
     *   "file_name": "Nebraska_Restaurant_Leads_2026.csv",
     *   "initial_status": "New Lead",
     *   "user_id": 1, (optional)
     *   "leads": [ ... ]
     * }
     */
    public function import(array $input): void
    {
        @ini_set('memory_limit', '1024M');
        @set_time_limit(300);

        $batchId = !empty($input['batch_id']) ? (int) $input['batch_id'] : null;
        $batchName = trim($input['batch_name'] ?? '');
        $fileName = trim($input['file_name'] ?? '');
        $initialStatus = trim($input['initial_status'] ?? 'New Lead');
        $userId = !empty($input['user_id']) ? (int) $input['user_id'] : null;
        $leads = $input['leads'] ?? [];
        $totalLeads = isset($input['total_leads']) ? (int) $input['total_leads'] : count($leads);
        $isLastChunk = !empty($input['is_last_chunk']);

        if (empty($batchName)) {
            $batchName = 'Batch_' . date('Ymd_His');
        }

        if (empty($fileName)) {
            $fileName = 'Direct_Import_' . date('Y-m-d');
        }

        if (!is_array($leads) || empty($leads)) {
            Response::error('No lead records provided for import.', 422);
        }

        try {
            $this->db->beginTransaction();

            // 1. Create the Batch record on first chunk (if batch_id not provided)
            if (empty($batchId)) {
                $stmtBatch = $this->db->prepare("
                    INSERT INTO `import_batches` 
                    (`batch_name`, `file_name`, `total_leads`, `initial_status`, `imported_by`) 
                    VALUES (?, ?, ?, ?, ?)
                ");
                $stmtBatch->execute([$batchName, $fileName, $totalLeads, $initialStatus, $userId]);
                $batchId = (int) $this->db->lastInsertId();
            } else {
                // If batch already exists, retrieve existing batch name if not passed
                if (empty($batchName)) {
                    $stmtGetBatch = $this->db->prepare("SELECT `batch_name` FROM `import_batches` WHERE `id` = ?");
                    $stmtGetBatch->execute([$batchId]);
                    $batchName = $stmtGetBatch->fetchColumn() ?: 'Batch_' . $batchId;
                }
            }

            // 2. High-performance Multi-Row Insert in chunks of 250
            $chunkSize = 250;
            $leadChunks = array_chunk($leads, $chunkSize);
            $inserted = 0;

            foreach ($leadChunks as $chunk) {
                $placeholders = [];
                $values = [];

                foreach ($chunk as $lead) {
                    $placeholders[] = "(?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
                    $status = !empty($lead['status']) ? $lead['status'] : $initialStatus;
                    $businessName = trim($lead['business_name'] ?? '');

                    if (empty($businessName)) {
                        $businessName = 'Unnamed Lead #' . ($inserted + 1);
                    }

                    array_push(
                        $values,
                        $batchId,
                        $batchName,
                        $status,
                        $businessName,
                        trim($lead['address'] ?? ''),
                        trim($lead['city'] ?? ''),
                        trim($lead['county'] ?? ''),
                        trim($lead['state'] ?? 'NE'),
                        trim($lead['zip_code'] ?? ''),
                        trim($lead['cell_number'] ?? ''),
                        trim($lead['fax'] ?? ''),
                        trim($lead['annual_sales'] ?? 'Less than 500,000'),
                        trim($lead['number_of_employees'] ?? '1 to 4'),
                        trim($lead['title'] ?? ''),
                        trim($lead['contact_name'] ?? ''),
                        trim($lead['website'] ?? ''),
                        trim($lead['sic_code'] ?? ''),
                        trim($lead['sic_description'] ?? ''),
                        trim($lead['naics_code'] ?? ''),
                        trim($lead['naics_description'] ?? '')
                    );
                    $inserted++;
                }

                $sql = "INSERT INTO `leads` (
                    `batch_id`, `batch_name`, `status`, `business_name`, `address`, 
                    `city`, `county`, `state`, `zip_code`, `cell_number`, 
                    `fax`, `annual_sales`, `number_of_employees`, `title`, `contact_name`, 
                    `website`, `sic_code`, `sic_description`, `naics_code`, `naics_description`
                ) VALUES " . implode(', ', $placeholders);

                $stmtBulk = $this->db->prepare($sql);
                $stmtBulk->execute($values);
            }

            // 3. If last chunk or single import, update batch total with exact count
            if ($isLastChunk || empty($input['batch_id'])) {
                $stmtCount = $this->db->prepare("SELECT COUNT(*) FROM `leads` WHERE `batch_id` = ?");
                $stmtCount->execute([$batchId]);
                $finalCount = (int) $stmtCount->fetchColumn();

                $stmtUpdate = $this->db->prepare("UPDATE `import_batches` SET `total_leads` = ? WHERE `id` = ?");
                $stmtUpdate->execute([$finalCount, $batchId]);
            }

            $this->db->commit();

            Response::success([
                'batch_id'       => $batchId,
                'batch_name'     => $batchName,
                'inserted_count' => $inserted,
                'total_leads'    => $totalLeads,
                'is_last_chunk'  => $isLastChunk
            ], "Successfully imported {$inserted} leads into the database.", 201);

        } catch (Exception $e) {
            if ($this->db->inTransaction()) {
                $this->db->rollBack();
            }
            Response::serverError('Failed to import leads: ' . $e->getMessage());
        }
    }

    /**
     * GET /leads
     * 
     * Returns leads with optional filtering and pagination.
     */
    public function index(): void
    {
        @ini_set('memory_limit', '1024M');
        @set_time_limit(300);

        $limit = isset($_GET['limit']) ? (int) $_GET['limit'] : 5000;
        $offset = isset($_GET['offset']) ? max((int) $_GET['offset'], 0) : 0;
        $status = $_GET['status'] ?? null;
        $city = $_GET['city'] ?? null;
        $county = $_GET['county'] ?? null;
        $state = $_GET['state'] ?? null;
        $annualSales = $_GET['annual_sales'] ?? null;
        $naicsCode = $_GET['naics_code'] ?? null;
        $naicsDesc = $_GET['naics_description'] ?? null;
        $sicDesc = $_GET['sic_description'] ?? null;
        $search = $_GET['search'] ?? null;
        $batchId = $_GET['batch_id'] ?? null;

        $conditions = [];
        $params = [];

        if (!empty($batchId)) {
            $conditions[] = "`batch_id` = ?";
            $params[] = (int) $batchId;
        }

        if (!empty($status) && $status !== 'All Statuses') {
            $conditions[] = "`status` = ?";
            $params[] = $status;
        }

        if (!empty($city) && $city !== 'All Cities') {
            $conditions[] = "`city` = ?";
            $params[] = $city;
        }

        if (!empty($county) && $county !== 'All Counties') {
            $conditions[] = "`county` = ?";
            $params[] = $county;
        }

        if (!empty($state) && $state !== 'All States') {
            $conditions[] = "`state` = ?";
            $params[] = $state;
        }

        if (!empty($annualSales) && $annualSales !== 'All Sales Ranges') {
            $conditions[] = "`annual_sales` = ?";
            $params[] = $annualSales;
        }

        if (!empty($naicsCode) && $naicsCode !== 'All NAICS Codes') {
            $conditions[] = "`naics_code` = ?";
            $params[] = $naicsCode;
        }

        if (!empty($naicsDesc) && $naicsDesc !== 'All NAICS Descriptions') {
            $conditions[] = "`naics_description` = ?";
            $params[] = $naicsDesc;
        }

        if (!empty($sicDesc) && $sicDesc !== 'All Industries') {
            $conditions[] = "`sic_description` = ?";
            $params[] = $sicDesc;
        }

        if (!empty($search)) {
            $conditions[] = "(`business_name` LIKE ? OR `contact_name` LIKE ? OR `cell_number` LIKE ? OR `city` LIKE ?)";
            $term = '%' . $search . '%';
            $params[] = $term;
            $params[] = $term;
            $params[] = $term;
            $params[] = $term;
        }

        $whereClause = !empty($conditions) ? 'WHERE ' . implode(' AND ', $conditions) : '';

        // Total count
        $countStmt = $this->db->prepare("SELECT COUNT(*) FROM `leads` {$whereClause}");
        $countStmt->execute($params);
        $totalCount = (int) $countStmt->fetchColumn();

        // Data rows
        $limitClause = $limit > 0 ? "LIMIT {$limit} OFFSET {$offset}" : "";
        $sql = "
            SELECT * FROM `leads` 
            {$whereClause} 
            ORDER BY `id` ASC 
            {$limitClause}
        ";
        $stmt = $this->db->prepare($sql);
        $stmt->execute($params);
        $leads = $stmt->fetchAll();

        Response::success([
            'total'  => $totalCount,
            'limit'  => $limit,
            'offset' => $offset,
            'leads'  => $leads
        ], 'Leads retrieved successfully');
    }

    /**
     * GET /leads/filters
     * 
     * Returns distinct filter options present in the database.
     */
    public function filters(): void
    {
        $cities = $this->db->query("SELECT DISTINCT `city` FROM `leads` WHERE `city` IS NOT NULL AND `city` != '' ORDER BY `city` ASC")->fetchAll(PDO::FETCH_COLUMN);
        $counties = $this->db->query("SELECT DISTINCT `county` FROM `leads` WHERE `county` IS NOT NULL AND `county` != '' ORDER BY `county` ASC")->fetchAll(PDO::FETCH_COLUMN);
        $states = $this->db->query("SELECT DISTINCT `state` FROM `leads` WHERE `state` IS NOT NULL AND `state` != '' ORDER BY `state` ASC")->fetchAll(PDO::FETCH_COLUMN);
        $statuses = $this->db->query("SELECT DISTINCT `status` FROM `leads` WHERE `status` IS NOT NULL AND `status` != '' ORDER BY `status` ASC")->fetchAll(PDO::FETCH_COLUMN);
        $industries = $this->db->query("SELECT DISTINCT `sic_description` FROM `leads` WHERE `sic_description` IS NOT NULL AND `sic_description` != '' ORDER BY `sic_description` ASC")->fetchAll(PDO::FETCH_COLUMN);
        $annualSales = $this->db->query("SELECT DISTINCT `annual_sales` FROM `leads` WHERE `annual_sales` IS NOT NULL AND `annual_sales` != '' ORDER BY `annual_sales` ASC")->fetchAll(PDO::FETCH_COLUMN);
        $naicsCodes = $this->db->query("SELECT DISTINCT `naics_code` FROM `leads` WHERE `naics_code` IS NOT NULL AND `naics_code` != '' ORDER BY `naics_code` ASC")->fetchAll(PDO::FETCH_COLUMN);
        $naicsDescs = $this->db->query("SELECT DISTINCT `naics_description` FROM `leads` WHERE `naics_description` IS NOT NULL AND `naics_description` != '' ORDER BY `naics_description` ASC")->fetchAll(PDO::FETCH_COLUMN);

        Response::success([
            'cities'             => $cities,
            'counties'           => $counties,
            'states'             => $states,
            'statuses'           => $statuses,
            'industries'         => $industries,
            'annual_sales'       => $annualSales,
            'naics_codes'        => $naicsCodes,
            'naics_descriptions' => $naicsDescs
        ], 'Filter options retrieved');
    }

    /**
     * GET /leads/stats
     * 
     * Returns summary dashboard counts.
     */
    public function stats(): void
    {
        $totalLeads = (int) $this->db->query("SELECT COUNT(*) FROM `leads`")->fetchColumn();
        $newLeads = (int) $this->db->query("SELECT COUNT(*) FROM `leads` WHERE `status` = 'New Lead'")->fetchColumn();
        $qualified = (int) $this->db->query("SELECT COUNT(*) FROM `leads` WHERE `status` = 'Qualified'")->fetchColumn();
        $contacted = (int) $this->db->query("SELECT COUNT(*) FROM `leads` WHERE `status` = 'Contacted'")->fetchColumn();

        Response::success([
            'total_leads'     => $totalLeads,
            'new_leads'       => $newLeads,
            'qualified_leads' => $qualified,
            'contacted_leads' => $contacted
        ], 'Lead metrics retrieved');
    }

    /**
     * GET /batches
     * 
     * Returns all past import batches for Import History.
     */
    public function batches(): void
    {
        $stmt = $this->db->query("
            SELECT b.*, u.username as imported_by_username 
            FROM `import_batches` b
            LEFT JOIN `users` u ON b.imported_by = u.id
            ORDER BY b.id DESC
        ");

        Response::success($stmt->fetchAll(), 'Import batches retrieved');
    }

    /**
     * GET /batches/{id}/leads
     * 
     * Returns all leads belonging to a specific batch.
     */
    public function batchLeads(int $batchId): void
    {
        if ($batchId <= 0) {
            Response::error('Invalid batch ID.', 422);
        }

        $stmt = $this->db->prepare("
            SELECT * FROM `leads` 
            WHERE `batch_id` = ? 
            ORDER BY `id` ASC
        ");
        $stmt->execute([$batchId]);
        $leads = $stmt->fetchAll();

        Response::success([
            'batch_id' => $batchId,
            'total'    => count($leads),
            'leads'    => $leads
        ], 'Batch leads retrieved successfully');
    }

    /**
     * DELETE /batches/{id} or POST /batches/delete
     * 
     * Deletes a batch and all associated leads permanently.
     */
    public function deleteBatch(int $batchId): void
    {
        if ($batchId <= 0) {
            Response::error('Invalid batch ID.', 422);
        }

        // Verify batch exists
        $stmt = $this->db->prepare("SELECT id, batch_name, total_leads FROM `import_batches` WHERE id = ?");
        $stmt->execute([$batchId]);
        $batch = $stmt->fetch();

        if (!$batch) {
            Response::notFound('Batch not found.');
        }

        try {
            $this->db->beginTransaction();

            // Explicitly delete leads for this batch (cascade also covers it)
            $delLeads = $this->db->prepare("DELETE FROM `leads` WHERE `batch_id` = ?");
            $delLeads->execute([$batchId]);
            $deletedLeadsCount = $delLeads->rowCount();

            // Delete the batch record
            $delBatch = $this->db->prepare("DELETE FROM `import_batches` WHERE `id` = ?");
            $delBatch->execute([$batchId]);

            $this->db->commit();

            Response::success([
                'batch_id'      => $batchId,
                'batch_name'    => $batch['batch_name'],
                'deleted_leads' => $deletedLeadsCount
            ], "Batch '{$batch['batch_name']}' and its {$deletedLeadsCount} leads were deleted successfully.");

        } catch (Exception $e) {
            if ($this->db->inTransaction()) {
                $this->db->rollBack();
            }
            Response::serverError('Failed to delete batch: ' . $e->getMessage());
        }
    }
}
