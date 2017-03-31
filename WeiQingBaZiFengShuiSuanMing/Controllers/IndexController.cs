﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EFDao.Entity;
using Common.Utility;
using Entity;
using System.Transactions;

namespace WeiQingBaZiFengShuiSuanMing.Controllers
{
    public class IndexController : Controller
    {
        /// <summary>
        /// 首页
        /// </summary>
        /// <returns></returns>
        [Filters.IsMobile]
        public ActionResult index(string key = "", string type = "", int page = 1)
        {
            // 查询八字简批的关键字段
            using (WeiQingEntities db = new WeiQingEntities())
            {
                // 搜索入口
                if (type != null && type.Length > 0 && key != null && key.Length > 0)
                {
                    switch (type)
                    {
                        case "ycls":
                            var yc = db.bazijianpi.Where(x => x.state == 1 && (x.bazi.Contains(key) || x.born_place.Contains(key) || x.name.Contains(key))).OrderByDescending(x => x.addtime).Take(12).ToList();
                            ViewData["baziList"] = yc;  // 搜索预测历史

                            var tz = (from t in db.title
                                      join u in db.user on t.uid equals u.id
                                      where t.state == 1
                                      orderby t.addtime descending
                                      select new TitleUserExt()
                                      {
                                          id = t.id,
                                          nick_name = u.nick_name,
                                          addtime = t.addtime,
                                          art_title = t.art_title,
                                          keywords = t.keywords,
                                          state = t.state,
                                          uid = t.uid
                                      }).Take(12).ToList();
                            ViewData["tzList"] = tz;    // 默认显示的帖子
                            break;
                        case "tytz":
                            var yc2 = db.bazijianpi.Where(x => x.state == 1).OrderByDescending(x => x.addtime).Take(12).ToList();
                            ViewData["baziList"] = yc2;  // 默认显示的预测历史

                            var tz2 = (from t in db.title
                                       join u in db.user on t.uid equals u.id
                                       where t.state == 1 && (t.art_title.Contains(key) || t.keywords.Contains(key))
                                       orderby t.addtime descending
                                       select new TitleUserExt()
                                       {
                                           id = t.id,
                                           nick_name = u.nick_name,
                                           addtime = t.addtime,
                                           art_title = t.art_title,
                                           keywords = t.keywords,
                                           state = t.state,
                                           uid = t.uid
                                       }).Take(12).ToList();
                            ViewData["tzList"] = tz2;    // 搜索帖子
                            break;
                        default:
                            return Redirect("/index/index");
                    }
                }
                else
                {
                    #region 搜索默认
                    var yc = db.bazijianpi.Where(x => x.state == 1).OrderByDescending(x => x.addtime).Take(12).ToList();
                    ViewData["baziList"] = yc;  // 默认显示的预测历史

                    var tz = (from t in db.title
                              join u in db.user on t.uid equals u.id
                              where t.state == 1
                              orderby t.addtime descending
                              select new TitleUserExt()
                              {
                                  id = t.id,
                                  nick_name = u.nick_name,
                                  addtime = t.addtime,
                                  art_title = t.art_title,
                                  keywords = t.keywords,
                                  state = t.state,
                                  uid = t.uid
                              }).Take(12).ToList();
                    ViewData["tzList"] = tz;    // 默认显示的帖子
                    #endregion
                }

                // 判断数量,是否显示更多按钮,打开列表页
                var ycCount = db.bazijianpi.Where(x => x.state == 1).Count();
                var tzCount = db.title.Where(x => x.state == 1).Count();
                ViewData["ycCount"] = ycCount;
                ViewData["tzCount"] = tzCount;
            }
            return View(ViewData["act"].ToString());
        }

        /// <summary>
        /// ajax加载电脑版的用户注册视图页
        /// </summary>
        /// <returns></returns>
        public ActionResult regView()
        {
            return View();
        }

        /// <summary>
        /// 用户注册的请求方法,注册成功,将用户保存到Session中
        /// </summary>
        /// <param name="u"></param>
        /// <returns></returns>
        [Filters.CheckLogin(IsLogin = false)]
        public ActionResult regUser(user u)
        {
            if (u != null)
            {
                SetNullString.setValues(u);
                if (Tools.getStrLength(u.nick_name) < 3)
                    return Content("用户名的长度必须大于3个字符");
                if (u.pwd.Length < 6)
                    return Content("密码必须大于6个字符");
                if (!Tools.IsEmail(u.email))
                    return Content("邮箱格式不正确");

                u.pwd = HashTools.SHA1_Hash(u.pwd);
                DateTime dt = DateTime.Now;
                u.reg_date = dt;
                int res = 0;


                try
                {
                    TransactionOptions transactionOption = new TransactionOptions();

                    //设置事务隔离级别
                    transactionOption.IsolationLevel = IsolationLevel.ReadCommitted;

                    // 设置事务超时时间为60秒
                    transactionOption.Timeout = new TimeSpan(0, 0, 60);

                    using (TransactionScope scope = new TransactionScope(TransactionScopeOption.Required, transactionOption))
                    {
                        using (WeiQingEntities db = new WeiQingEntities())
                        {
                            var count = db.user.Where(p => p.nick_name.Equals(u.nick_name) || p.email.Equals(u.email)).Count();
                            if (count > 0)
                            {
                                return Content("此用户名或者邮箱已被注册");
                            }
                            u.is_admin = false;
                            db.user.Add(u);
                            res = db.SaveChanges();   // 创建用户
                            if (res == 0)
                            {
                                return Content("注册失败");
                            }
                            var user = db.user.Where(p => p.nick_name.Equals(u.nick_name)).FirstOrDefault();
                            string ip = Tools.GetRealIP();
                            login_log log = new login_log() { uid = (int)user.id, login_ip = ip, login_time = dt };
                            db.login_log.Add(log);
                            res = db.SaveChanges();
                            if (res > 0) { Session["user"] = user; scope.Complete(); }
                            else
                            {
                                return Content("保存登录记录时出现异常");
                            }
                            return Content(res.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    return Content("后台出现错误");
                }

            }
            return Content("没有获取到数据");
        }

        /// <summary>
        /// 用户登录,登录成功更新用户最后登录的时间,保存到session
        /// </summary>
        /// <param name="u"></param>
        /// <returns></returns>
        [Filters.CheckLogin(IsLogin = false)]
        public ActionResult login(user u)
        {
            if (u != null)
            {
                SetNullString.setValues(u);
                if (u.nick_name.Length >= 3 && u.pwd.Length >= 6)
                {
                    u.pwd = HashTools.SHA1_Hash(u.pwd);

                    try
                    {
                        using (WeiQingEntities db = new WeiQingEntities())
                        {
                            var user = db.user.Where(p => (p.nick_name.Equals(u.nick_name) || p.email.Equals(u.nick_name))
                            && p.pwd.Equals(u.pwd)).FirstOrDefault();
                            if (user != null && (user.nick_name.Equals(u.nick_name) || user.email.Equals(u.nick_name)))
                            {
                                string ip = Tools.GetRealIP();
                                login_log log = new login_log() { uid = (int)user.id, login_ip = ip, login_time = DateTime.Now };
                                db.login_log.Add(log);
                                db.SaveChanges();
                                Session["user"] = user;
                                return Content("1");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        return Content("后台出现错误:" + ex.Message);
                    }

                }
                return Content("用户名和密码不正确");
            }
            return Content("参数错误");
        }

        /// <summary>
        /// 注销登录
        /// </summary>
        /// <returns></returns>
        public ActionResult logout()
        {
            Session["user"] = null;
            return Redirect("/index/index");
        }

        /// <summary>
        /// 找回密码的视图页
        /// </summary>
        /// <returns></returns>
        public ActionResult forgetPwd()
        {
            return View();
        }

        /// <summary>
        /// 发送邮件,找回密码
        /// </summary>
        /// <param name="u"></param>
        /// <returns></returns>
        [Filters.CheckLogin(IsLogin = false)]
        public ActionResult getPwd(user u)
        {
            if (u != null)
            {
                SetNullString.setValues(u);
                if (u.nick_name.Length >= 3 && Tools.IsEmail(u.email))
                {
                    try
                    {
                        using (WeiQingEntities db = new WeiQingEntities())
                        {
                            var user = db.user.Where(p => p.nick_name.Equals(u.nick_name) && p.email.Equals(u.email)).FirstOrDefault();
                            // 检查用户名和邮箱是否匹配
                            if (user != null && u.nick_name.Equals(user.nick_name))
                            {
                                DateTime dt = DateTime.Now;
                                string ip = Tools.GetRealIP();  // 获取客户端ip

                                // 检查当前 uid 一周之内是否已经找回过密码, 同一个ip一天之内之内找回3次密码
                                var t1 = dt.AddDays(-7);
                                var gpl_uid = db.getpwdlog.Where(p => p.uid == user.id && p.log_time > t1).Count();
                                if (gpl_uid > 0)
                                {
                                    return Content("一个星期之内只能找回一次密码");
                                }
                                var t2 = dt.AddHours(-24);
                                var gpl_ip = db.getpwdlog.Where(p => p.ip_address.Equals(ip) && p.log_time > t2).Count();
                                if (gpl_ip >= 3)
                                {
                                    return Content("同一个ip地址一天之内只能找回3次密码");
                                }

                                string newpwd = Tools.getRandomStr();
                                string res = Tools.SendEmail(u.email, "您的密码是:" + newpwd);   // 失败返回错误信息
                                if ("发送成功".Equals(res))
                                {
                                    var chPwdLog = new getpwdlog() { uid = (Int32)user.id, ip_address = ip, nick_name = user.nick_name, log_time = dt };
                                    db.getpwdlog.Add(chPwdLog); // 修改密码的日志
                                    user.pwd = HashTools.SHA1_Hash(newpwd);
                                    db.SaveChanges();   // 修改密码
                                }
                                return Content(res);
                            }
                            return Content("用户名或者邮箱不正确");
                        }
                    }
                    catch (Exception ex)
                    {
                        return Content("后台出现错误:" + ex.Message);
                    }
                }
            }
            return Content("参数错误");
        }

        /// <summary>
        /// 修改密码的视图页
        /// </summary>
        /// <returns></returns>
        public ActionResult changePwd()
        {
            return View();
        }

        /// <summary>
        /// 修改密码
        /// </summary>
        /// <param name="u"></param>
        /// <returns></returns>
        [Filters.CheckLogin]
        public ActionResult updatePwd(UserExt u)
        {
            if (u != null)
            {
                SetNullString.setValues(u);
                if (u.nick_name.Length >= 3 && u.pwd.Length >= 6 && u.newpwd.Length >= 6 && !u.pwd.Equals(u.newpwd))
                {
                    try
                    {
                        string old_hash_pwd = HashTools.SHA1_Hash(u.pwd);   // 旧的hash密码
                        using (WeiQingEntities db = new WeiQingEntities())
                        {
                            var user = db.user.Where(p => p.nick_name.Equals(u.nick_name) && p.pwd.Equals(old_hash_pwd)).FirstOrDefault();
                            if (user != null && u.nick_name.Equals(user.nick_name))
                            {
                                user.pwd = HashTools.SHA1_Hash(u.newpwd);
                                int res = db.SaveChanges();   // 修改密码
                                if (res > 0) { Session["user"] = null; }
                                return Content(res.ToString());
                            }
                            return Content("旧密码不正确");
                        }
                    }
                    catch (Exception ex)
                    {
                        return Content("后台出现错误");
                    }
                }
                return Content("修改失败");
            }
            return Content("参数错误");
        }

        /// <summary>
        /// 电脑版的八字预测说明页
        /// </summary>
        /// <returns></returns>
        public ActionResult BaZiYuCeExplain()
        {
            return View();
        }

        /// <summary>
        /// 八字简批上传的八字
        /// </summary>
        /// <returns></returns>
        public ActionResult BaZiJianPi(bazijianpi model)
        {
            if (model != null)
            {
                if (model.born_date == DateTime.MinValue)
                {
                    return Content("出生时间参数错误");
                }

                try
                {
                    SetNullString.setValues(model, false);
                    using (WeiQingEntities db = new WeiQingEntities())
                    {
                        model.addtime = DateTime.Now;
                        db.bazijianpi.Add(model);
                        int res = db.SaveChanges();
                        if (res > 0)
                        {
                            var t1 = model.addtime.Date;
                            var count = db.bazijianpi.Where(p => p.addtime > t1).Count();
                            return Content(string.Format("提交成功,您前面还有{0}位求测者,请耐心等待", count - 1));
                        }
                    }
                }
                catch (Exception ex)
                {
                    return Content("后台出现错误:" + ex.Message);
                }

            }
            return Content("参数错误");
        }

        /// <summary>
        /// 管理员登录页
        /// </summary>
        /// <returns></returns>
        public ActionResult admin()
        {
            return View();
        }

        /// <summary>
        /// 验证用户是否具有管理员权限
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public ActionResult adminLogin(user u)
        {
            if (u != null)
            {
                // 查询用户的数据,判断权限
                SetNullString.setValues(u);
                if (u.nick_name.Length > 0 && u.pwd.Length > 0)
                {
                    u.pwd = HashTools.SHA1_Hash(u.pwd);
                    using (WeiQingEntities db = new WeiQingEntities())
                    {
                        var user = db.user.Where(x => (x.nick_name.Equals(u.nick_name) || x.email.Equals(u.nick_name)) && x.pwd.Equals(u.pwd)).FirstOrDefault();
                        if (user != null && user.is_admin && user.id > 0)
                        {
                            Session["user"] = user;
                            string ip = Tools.GetRealIP();
                            login_log log = new login_log() { uid = (int)user.id, login_ip = ip, login_time = DateTime.Now };
                            db.login_log.Add(log);
                            db.SaveChanges();
                            return Content("1");
                        }
                    }
                }
                return Content("-2");
            }
            return Content("-1");
        }

        /// <summary>
        /// 八字简批
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult bzjp(int id = 0)
        {
            if (id > 0)
            {
                using (WeiQingEntities db = new WeiQingEntities())
                {
                    var m = db.bazijianpi.Where(x => x.id == id).FirstOrDefault();
                    if (m != null && m.id > 0)
                    {
                        ViewData["m"] = m;
                        return View();
                    }
                }
            }
            return Redirect("/index/index");
        }

        /// <summary>
        /// 创建新帖的视图页
        /// </summary>
        /// <returns></returns>
        [Filters.CheckLogin(IsAjax = false, IsPost = false)]
        public ActionResult createSub()
        {
            return View();
        }

        /// <summary>
        /// 创建帖子标题
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [ValidateInput(false)]
        [Filters.CheckLogin]
        public ActionResult createTitle(TitleExt model)
        {
            if (model != null)
            {
                SetNullString.setValues(model);
                int res = 0;
                DateTime dt = DateTime.Now;

                try
                {
                    var u = (user)Session["user"];
                    title t = new title() { uid = u.id, addtime = dt, art_title = model.art_title, keywords = model.keywords, state = 1 };

                    TransactionOptions transOpt = new TransactionOptions();

                    //设置事务隔离级别
                    transOpt.IsolationLevel = IsolationLevel.ReadCommitted;

                    // 设置事务超时时间为60秒
                    transOpt.Timeout = new TimeSpan(0, 0, 60);

                    using (TransactionScope scope = new TransactionScope(TransactionScopeOption.Required, transOpt))
                    {
                        using (WeiQingEntities db = new WeiQingEntities())
                        {
                            db.title.Add(t);
                            res = db.SaveChanges(); // 保存帖子标题
                            if (res > 0)
                            {
                                var tid = db.title.Where(x => x.uid == u.id).Max(x => x.id);
                                tiezi tz = new tiezi() { tid = tid, addtime = dt, uid = u.id, content = model.content, floor = 1, state = 1, uname = u.nick_name };
                                db.tiezi.Add(tz);
                                res = db.SaveChanges(); // 保存帖子的内容,一楼
                                if (res > 0)
                                {
                                    scope.Complete();
                                    return Content(res.ToString());
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    return Content(ex.Message);
                }

            }
            return Content("0");
        }

        /// <summary>
        /// 查看帖子详情,一次显示12条
        /// </summary>
        /// <param name="id">帖子表的外键tid (title表的主键)</param>
        /// <param name="page">页码</param>
        /// <returns></returns>
        public ActionResult article(int id = 0, int page = 1)
        {
            if (id > 0)
            {
                using (WeiQingEntities db = new WeiQingEntities())
                {
                    var title = (from t in db.title join us in db.user on t.uid equals us.id where t.id == id && t.state == 1 select new TitleExt() { uname = us.nick_name, id = t.id, addtime = t.addtime, art_title = t.art_title, keywords = t.keywords, state = t.state, uid = t.uid }).FirstOrDefault();
                    ViewData["title"] = title;  // 获取帖子标题
                    ViewData["tid"] = title.id; // 回帖需要用的参数

                    var q = (from tit in db.title join tz in db.tiezi on tit.id equals tz.tid where tz.state == 1 && tit.state == 1 && tz.tid == id orderby tz.id select new TieZiExt() { id = tz.id, addtime = tz.addtime, content = tz.content, floor = tz.floor, state = tz.state, tid = tz.tid, uid = tz.uid, uname = tz.uname });
                    // ViewData["sql"] = q.ToString();
                    var p = new EFPaging<TieZiExt>();

                    var tzList = p.getPageList(q, "/index/article/" + id, page, 12);    // 获取帖子内容列表, 再获取回复内容列表
                    if (tzList != null && tzList.Count > 0)
                    {
                        foreach (var item in tzList)
                        {
                            var replyList = (from tz in db.tiezi join rep in db.tzreply on tz.id equals rep.tzid where tz.state == 1 && rep.state == 1 && rep.tzid == item.id orderby rep.id select rep).ToList();
                            item.replyList = replyList; // 根据帖子内容的id获取回复的内容列表
                        }
                    }
                    ViewData["tzList"] = tzList;
                    ViewData["url"] = p.pageUrl;
                }
            }
            return View();
        }

        /// <summary>
        /// 回复帖子,需要判断当前帖子是否被屏蔽,以及当前楼层是否屏蔽
        /// </summary>
        /// <returns></returns>
        [Filters.CheckLogin]
        public ActionResult tzReply(tzreply model)
        {
            if (model != null && model.tzid > 0)
            {
                SetNullString.setValues(model);
                if (model.content.Length == 0)
                    return Content("-2");   // 用户没有正确输入内容
                
                var u = (user)Session["user"];
                model.uid = (int)u.id;
                model.addtime = DateTime.Now;
                model.state = 1;    // 显示,被举报之后可能会屏蔽
                model.uname = u.nick_name;
                using(WeiQingEntities db=new WeiQingEntities())
                {
                    var tzuid = db.tiezi.Where(x => x.id == model.tzid).Select(x => x.uid).FirstOrDefault();    // 回复的帖子的uid
                    if (tzuid == u.id)
                    {
                        return Content("不能回复自己的内容哦,请在底部发布内容");
                    }
                    db.tzreply.Add(model);
                    return Content(db.SaveChanges().ToString());
                }
            }
            return Content("-1");
        }

        /// <summary>
        /// 根据指定楼层的帖子id进行举报, 返回视图页
        /// </summary>
        /// <param name="id">指定楼层的帖子id</param>
        /// <returns></returns>
        public ActionResult jubao_tiezi(int id = 0)
        {
            if (id > 0)
            {
                ViewData["tzid"] = id;
                return View();
            }
            return new EmptyResult();
        }

        /// <summary>
        /// 举报帖子内容提交到数据库, 如果已经被举报过, 直接返回举报成功, 如果同一个ip同一天大量举报, 返回失败
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [Filters.CheckLogin(IsAjax =true,IsLogin =false,IsPost =true)]
        public ActionResult jubao_sub(tiezi_jubao model)
        {
            if (model != null && model.tzid > 0 && model.reason != null)
            {
                model.addtime = DateTime.Now;
                model.ip = Tools.GetRealIP();
                using(WeiQingEntities db=new WeiQingEntities())
                {
                    var t1 = DateTime.Now.Date;
                    var ipCount = db.tiezi_jubao.Where(x => x.addtime > t1 && x.ip.Equals(model.ip)).Count();
                    if (ipCount >= 10)
                    {
                        return Content("-1");
                    }

                    var count = db.tiezi_jubao.Where(x => x.tzid == model.tzid).Count();    // 此帖子是否已经举报过
                    if (count > 0)
                    {
                        return Content("2");
                    }

                    db.tiezi_jubao.Add(model);
                    return Content(db.SaveChanges().ToString());
                }
            }
            return Content("-1");
        }

        /// <summary>
        /// 回帖的请求地址
        /// </summary>
        /// <returns></returns>
        [ValidateInput(false)]
        [Filters.CheckLogin]
        public ActionResult huitie(tiezi model)
        {
            if (model != null && model.tid > 0 && model.content != null && model.content.Length > 0)
            {
                model.addtime = DateTime.Now;
                var user = (user)Session["user"];
                model.uid = user.id;
                model.state = 1;
                model.uname = user.nick_name;
                // 获取楼层
                using (WeiQingEntities db = new WeiQingEntities())
                {
                    var floor = db.tiezi.Where(x => x.tid == model.tid).Max(x => x.floor);
                    model.floor = floor + 1;    // 当前楼层加1
                    db.tiezi.Add(model);
                    return Content(db.SaveChanges().ToString());
                }
            }
            return Content("-1");
        }

        /// <summary>
        /// 分页查看预测历史的列表
        /// </summary>
        /// <param name="key"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        public ActionResult ycList(string key = "", int page = 1)
        {
            using (WeiQingEntities db = new WeiQingEntities())
            {
                if (key != null && key.Length > 0)
                {
                    var q = db.bazijianpi.Where(x => x.state == 1 && (x.bazi.Contains(key) || x.born_place.Contains(key) || x.name.Contains(key))).OrderByDescending(x => x.addtime);
                    var p = new EFPaging<bazijianpi>();
                    var ycList = p.getPageList(q, "/index/yclist", page, 12);
                    ViewData["ycList"] = ycList;
                    ViewData["url"] = p.pageUrl;
                }
                else
                {
                    var q = db.bazijianpi.Where(x => x.state == 1).OrderByDescending(x => x.addtime);
                    var p = new EFPaging<bazijianpi>();
                    var ycList = p.getPageList(q, "/index/yclist", page, 12);
                    ViewData["ycList"] = ycList;
                    ViewData["url"] = p.pageUrl;
                }
            }
            return View();
        }

        /// <summary>
        /// 分页查看帖子列表
        /// </summary>
        /// <returns></returns>
        public ActionResult artList()
        {
            return View();
        }

    }
}