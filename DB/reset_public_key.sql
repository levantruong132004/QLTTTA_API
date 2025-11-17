-- Script xóa public key trung tâm để tạo lại
-- CHỈ chạy script này khi:
-- 1. Hệ thống mới triển khai
-- 2. Chưa có hóa đơn nào được ký
-- 3. Private key cũ bị mất/lộ và cần tạo lại

-- Kiểm tra xem có public key không
SELECT ID_TTTA, TEN_TRUNG_TAM, 
       CASE WHEN KHOA_CONG_PEM IS NULL THEN 'CHƯA CÓ' ELSE 'ĐÃ CÓ' END AS TRANG_THAI,
       LENGTH(KHOA_CONG_PEM) AS DO_DAI_KHOA
FROM QLTT_ADMIN.TTTA
WHERE ID_TTTA = 1;

-- XÓA public key cũ (CẢNH BÁO: Các hóa đơn cũ sẽ không verify được nữa)
UPDATE QLTT_ADMIN.TTTA 
SET KHOA_CONG_PEM = NULL,
    UPDATED_AT = SYSDATE
WHERE ID_TTTA = 1;

COMMIT;

-- Kiểm tra lại sau khi xóa
SELECT ID_TTTA, TEN_TRUNG_TAM, 
       CASE WHEN KHOA_CONG_PEM IS NULL THEN 'ĐÃ XÓA - CÓ THỂ TẠO MỚI' ELSE 'VẪN CÒN' END AS TRANG_THAI
FROM QLTT_ADMIN.TTTA
WHERE ID_TTTA = 1;
