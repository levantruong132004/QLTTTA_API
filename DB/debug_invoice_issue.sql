-- Script debug: Kiểm tra hóa đơn và trạng thái thực tế
-- Chạy script này để xem tình trạng hóa đơn hiện tại

-- 1. Kiểm tra đơn đăng ký ID = 10 (từ ảnh)
SELECT 
    dk.ID_DANG_KY,
    dk.MA_DANG_KY, 
    dk.TRANG_THAI as TRANG_THAI_DON,
    hv.HO_TEN,
    kh.TEN_KHOA_HOC,
    lh.TEN_LOP_HOC,
    dk.NGAY_DANG_KY
FROM DON_DANG_KY dk
JOIN HOC_VIEN hv ON dk.ID_HOC_VIEN = hv.ID_HOC_VIEN  
JOIN LOP_HOC lh ON dk.ID_LOP_HOC = lh.ID_LOP_HOC
JOIN KHOA_HOC kh ON lh.ID_KHOA_HOC = kh.ID_KHOA_HOC
WHERE dk.ID_DANG_KY IN (10, 11, 12, 13, 14, 15)
ORDER BY dk.ID_DANG_KY;

-- 2. Kiểm tra hóa đơn liên quan đến các đơn này
SELECT 
    hd.ID_HOA_DON,
    hd.MA_HOA_DON,
    hd.ID_DANG_KY,
    hd.NGAY_TAO,
    hd.NGAY_HET_HAN,
    hd.SO_TIEN,
    hd.TRANG_THAI,
    hd.CHU_KY_BASE64,
    NVL(hd.DA_IN, 0) as DA_IN,
    dk.MA_DANG_KY
FROM HOA_DON hd
JOIN DON_DANG_KY dk ON hd.ID_DANG_KY = dk.ID_DANG_KY
WHERE dk.ID_DANG_KY IN (10, 11, 12, 13, 14, 15)
ORDER BY hd.ID_DANG_KY;

-- 3. Kiểm tra có hóa đơn "ma" nào không
SELECT COUNT(*) as TONG_SO_HOA_DON FROM HOA_DON;

SELECT 
    hd.ID_HOA_DON,
    hd.MA_HOA_DON,
    hd.ID_DANG_KY,
    dk.MA_DANG_KY,
    hd.NGAY_TAO
FROM HOA_DON hd
LEFT JOIN DON_DANG_KY dk ON hd.ID_DANG_KY = dk.ID_DANG_KY
ORDER BY hd.ID_HOA_DON DESC
FETCH FIRST 10 ROWS ONLY;

-- 4. Kiểm tra API endpoint có đang cache sai không
-- (Check qua API để đảm bảo)