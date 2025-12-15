alter profile ttta_user_profile limit
   failed_login_attempts 5
   password_lock_time ( 5 / 1440 ) -- nếu sai pass 5 lần thì khóa tài khoản 5 phút
   password_life_time 90       -- phải đổi mật khẩu sau 90 ngày
   password_grace_time 7       -- có 7 ngày gia hạn nếu không đổi mật khẩu
   password_reuse_time 365     -- Không cho phép tái sử dụng mật khẩu trong vòng 365 ngày kể từ lần sử dụng trước đó.
   password_reuse_max 5        -- không dùng lại 5 mật khẩu gần nhất
   idle_time 30                -- idle 30 phút (nếu không hoạt động 30p thì tự động logout)
   sessions_per_user 3         -- tối đa 3 phiên đồng thời