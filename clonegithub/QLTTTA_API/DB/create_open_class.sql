-- Generic parameterized wrapper: pass course code as the first argument
-- Usage examples (SQL*Plus/SQLcl/SQL Developer):
--   @create_open_class.sql GT_CB
--   @create_open_class.sql TOEIC550
--   @create_open_class.sql IELTS55
SET SERVEROUTPUT ON
DEFINE COURSE_CODE='&1'
@create_open_class_for_course.sql
