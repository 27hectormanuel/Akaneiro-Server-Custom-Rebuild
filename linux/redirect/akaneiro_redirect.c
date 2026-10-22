/*
 * akaneiro_redirect.so - LD_PRELOAD shim that does the job of the Windows hosts file entry
 * "127.0.0.1 mw.angry-red.com" and of the privileged port 80, without root.
 *
 *  - Name lookups (getaddrinfo, gethostbyname*) of the redirected host names resolve to
 *    AKANEIRO_SERVER_IP instead.
 *  - connect() to AKANEIRO_SERVER_IP:80 goes to AKANEIRO_SERVER_IP:AKANEIRO_HTTP_PORT, so the
 *    server's web service can listen on an unprivileged port.
 *
 * The game client itself is not changed. Settings (environment variables):
 *   AKANEIRO_SERVER_IP        address of the server (default 127.0.0.1)
 *   AKANEIRO_HTTP_PORT        port of the server's web service (default 8880)
 *   AKANEIRO_REDIRECT_HOSTS   comma separated host names to redirect (default mw.angry-red.com)
 *   AKANEIRO_FONT_DIR         extra folder searched with /usr/share/fonts (see "Fonts" below)
 *   AKANEIRO_REDIRECT_DEBUG   set to 1 to log every redirect to stderr
 */
#define _GNU_SOURCE
#include <dlfcn.h>
#include <ftw.h>
#include <glob.h>
#include <netdb.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <strings.h>
#include <sys/socket.h>
#include <sys/stat.h>

#define DEFAULT_IP "127.0.0.1"
#define DEFAULT_HTTP_PORT 8880
#define DEFAULT_HOSTS "mw.angry-red.com"

static const char *server_ip(void)
{
    const char *ip = getenv("AKANEIRO_SERVER_IP");
    return ip && *ip ? ip : DEFAULT_IP;
}

static int http_port(void)
{
    const char *p = getenv("AKANEIRO_HTTP_PORT");
    int port = p && *p ? atoi(p) : DEFAULT_HTTP_PORT;
    return port > 0 && port < 65536 ? port : DEFAULT_HTTP_PORT;
}

static int debug(void)
{
    const char *d = getenv("AKANEIRO_REDIRECT_DEBUG");
    return d && *d && *d != '0';
}

/* True if name is one of the comma separated AKANEIRO_REDIRECT_HOSTS (case-insensitive). */
static int is_redirected(const char *name)
{
    if (!name)
        return 0;
    const char *list = getenv("AKANEIRO_REDIRECT_HOSTS");
    if (!list || !*list)
        list = DEFAULT_HOSTS;
    size_t len = strlen(name);
    while (*list) {
        const char *end = strchr(list, ',');
        size_t n = end ? (size_t)(end - list) : strlen(list);
        if (n == len && strncasecmp(list, name, n) == 0)
            return 1;
        if (!end)
            break;
        list = end + 1;
    }
    return 0;
}

static const char *map_name(const char *name, const char *func)
{
    if (!is_redirected(name))
        return name;
    if (debug())
        fprintf(stderr, "[akaneiro_redirect] %s(%s) -> %s\n", func, name, server_ip());
    return server_ip();
}

#define REAL(name) \
    static __typeof__(name) *real_##name; \
    if (!real_##name) real_##name = (__typeof__(name) *)dlsym(RTLD_NEXT, #name)

int getaddrinfo(const char *node, const char *service, const struct addrinfo *hints, struct addrinfo **res)
{
    REAL(getaddrinfo);
    return real_getaddrinfo(map_name(node, "getaddrinfo"), service, hints, res);
}

struct hostent *gethostbyname(const char *name)
{
    REAL(gethostbyname);
    return real_gethostbyname(map_name(name, "gethostbyname"));
}

struct hostent *gethostbyname2(const char *name, int af)
{
    REAL(gethostbyname2);
    return real_gethostbyname2(map_name(name, "gethostbyname2"), af);
}

int gethostbyname_r(const char *name, struct hostent *ret, char *buf, size_t buflen,
                    struct hostent **result, int *h_errnop)
{
    REAL(gethostbyname_r);
    return real_gethostbyname_r(map_name(name, "gethostbyname_r"), ret, buf, buflen, result, h_errnop);
}

int gethostbyname2_r(const char *name, int af, struct hostent *ret, char *buf, size_t buflen,
                     struct hostent **result, int *h_errnop)
{
    REAL(gethostbyname2_r);
    return real_gethostbyname2_r(map_name(name, "gethostbyname2_r"), af, ret, buf, buflen, result, h_errnop);
}

/*
 * Fonts. The game's speech bubbles use the dynamic font "Arial", which this Unity version looks
 * up by family name among the files under /usr/share/fonts only (SteamOS has no Arial there, and
 * the folder is read-only). Scans of /usr/share/fonts also cover AKANEIRO_FONT_DIR, a folder with
 * an Arial replacement (Proton's share/fonts by default).
 */
static int is_system_font_dir(const char *path)
{
    return path && strncmp(path, "/usr/share/fonts", 16) == 0 && (path[16] == 0 || path[16] == '/');
}

static const char *extra_font_dir(void)
{
    const char *d = getenv("AKANEIRO_FONT_DIR");
    return d && *d ? d : NULL;
}

int ftw(const char *dir, int (*fn)(const char *, const struct stat *, int), int nopenfd)
{
    REAL(ftw);
    int r = real_ftw(dir, fn, nopenfd);
    if (r == 0 && is_system_font_dir(dir) && extra_font_dir()) {
        if (debug())
            fprintf(stderr, "[akaneiro_redirect] ftw(%s) + %s\n", dir, extra_font_dir());
        r = real_ftw(extra_font_dir(), fn, nopenfd);
    }
    return r;
}

int glob(const char *pattern, int flags, int (*errfunc)(const char *, int), glob_t *pglob)
{
    static __typeof__(glob) *real_glob;
    if (!real_glob) {
#if defined(__i386__)
        /* The game links against the original 32-bit glob; keep calling that version. */
        real_glob = (__typeof__(glob) *)dlvsym(RTLD_NEXT, "glob", "GLIBC_2.0");
#endif
        if (!real_glob)
            real_glob = (__typeof__(glob) *)dlsym(RTLD_NEXT, "glob");
    }
    int r = real_glob(pattern, flags, errfunc, pglob);
    if (is_system_font_dir(pattern) && extra_font_dir() && (r == 0 || r == GLOB_NOMATCH)) {
        char extra[4096];
        snprintf(extra, sizeof extra, "%s%s", extra_font_dir(), pattern + 16);
        if (debug())
            fprintf(stderr, "[akaneiro_redirect] glob(%s) + %s\n", pattern, extra);
        int r2 = real_glob(extra, flags | (r == 0 ? GLOB_APPEND : 0), errfunc, pglob);
        if (r == GLOB_NOMATCH)
            r = r2;
    }
    return r;
}

int connect(int fd, const struct sockaddr *addr, socklen_t len)
{
    REAL(connect);
    if (addr && addr->sa_family == AF_INET && len >= (socklen_t)sizeof(struct sockaddr_in)) {
        const struct sockaddr_in *in = (const struct sockaddr_in *)addr;
        struct in_addr target;
        if (ntohs(in->sin_port) == 80 && inet_pton(AF_INET, server_ip(), &target) == 1
            && in->sin_addr.s_addr == target.s_addr) {
            struct sockaddr_in moved = *in;
            moved.sin_port = htons((unsigned short)http_port());
            if (debug())
                fprintf(stderr, "[akaneiro_redirect] connect(%s:80) -> port %d\n", server_ip(), http_port());
            return real_connect(fd, (const struct sockaddr *)&moved, sizeof moved);
        }
    }
    return real_connect(fd, addr, len);
}
