ALTER TABLE HOA_DON ADD (
    SIGNED_PDF_HASH     VARCHAR2(128),
    SIGNED_PDF_VERSION  VARCHAR2(20),
    SIGNED_PDF_PATH     VARCHAR2(255)
);

COMMENT ON COLUMN HOA_DON.SIGNED_PDF_HASH IS 'SHA-256 hash of the final signed PDF for integrity checking';
COMMENT ON COLUMN HOA_DON.SIGNED_PDF_VERSION IS 'Signing profile version (e.g., PAdES)';
COMMENT ON COLUMN HOA_DON.SIGNED_PDF_PATH IS 'Relative path to the signed PDF stored on disk or blob reference';
