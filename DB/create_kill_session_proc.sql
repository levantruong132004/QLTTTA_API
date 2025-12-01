
CREATE OR REPLACE PROCEDURE SP_KILL_USER_SESSION (
    p_username IN VARCHAR2
) AS
    TYPE t_session_rec IS RECORD (sid NUMBER, serial# NUMBER, status VARCHAR2(20));
    TYPE t_session_cur IS REF CURSOR;
    v_cur t_session_cur;
    v_rec t_session_rec;
    v_sql VARCHAR2(1000);
    v_kill_sql VARCHAR2(1000);
    v_count NUMBER := 0;
    v_missing_grant EXCEPTION;
    PRAGMA EXCEPTION_INIT(v_missing_grant, -942);
BEGIN
    IF p_username IS NULL OR LENGTH(TRIM(p_username)) = 0 THEN
        RETURN;
    END IF;

    v_sql := 'SELECT sid, serial#, status FROM v$session WHERE UPPER(username) = UPPER(TRIM(:1))';
    
    BEGIN
        OPEN v_cur FOR v_sql USING p_username;
        LOOP
            FETCH v_cur INTO v_rec;
            EXIT WHEN v_cur%NOTFOUND;
            
            v_count := v_count + 1;
            
            BEGIN
                v_kill_sql := 'ALTER SYSTEM DISCONNECT SESSION ''' || v_rec.sid || ',' || v_rec.serial# || ''' IMMEDIATE';
                EXECUTE IMMEDIATE v_kill_sql;
            EXCEPTION 
                WHEN OTHERS THEN
                    BEGIN
                        v_kill_sql := 'ALTER SYSTEM KILL SESSION ''' || v_rec.sid || ',' || v_rec.serial# || ''' IMMEDIATE';
                        EXECUTE IMMEDIATE v_kill_sql;
                    EXCEPTION WHEN OTHERS THEN NULL; END;
            END;
        END LOOP;
        CLOSE v_cur;
    EXCEPTION 
        WHEN v_missing_grant THEN
            RAISE_APPLICATION_ERROR(-20003, 'Thiếu quyền: Cần chạy "GRANT SELECT ON V_$SESSION TO QLTT_ADMIN" bằng tài khoản SYS.');
    END;
EXCEPTION 
    WHEN OTHERS THEN
        IF v_cur%ISOPEN THEN CLOSE v_cur; END IF;
        RAISE;
END;
/
