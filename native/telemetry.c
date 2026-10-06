// Clustr Galaxy Linux qualification telemetry. No network or user storage access.
#define _GNU_SOURCE
#include <dlfcn.h>
#include <link.h>
#include <dirent.h>
#include <stdatomic.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <limits.h>
#include <sys/resource.h>
#include <sys/stat.h>
#include <sys/sysmacros.h>

static _Atomic uint64_t allocated_bytes;
static uint32_t (*object_size)(void *);
static int allocation_available;
static void *mono_library;
static void allocation(void *profiler, void *object) {
    (void)profiler;
    atomic_fetch_add_explicit(&allocated_bytes, object_size(object), memory_order_relaxed);
}
static int find_mono(struct dl_phdr_info *info, size_t size, void *data) {
    (void)size; (void)data;
    if (strstr(info->dlpi_name, "libmonobdwgc-2.0.so")) {
        mono_library = dlopen(info->dlpi_name, RTLD_NOW | RTLD_NOLOAD);
        return mono_library != NULL;
    }
    return 0;
}
static int profiling_requested(void) {
    char args[16384]; FILE *file = fopen("/proc/self/cmdline", "rb");
    if (!file) return 0;
    size_t size = fread(args, 1, sizeof(args)-1, file); fclose(file); args[size] = 0;
    for (size_t i=0; i<size; i+=strlen(args+i)+1)
        if (!strncmp(args+i, "--clustr-beta-profile", 21) || !strcmp(args+i, "--clustr-ui-performance")) return 1;
    return 0;
}
__attribute__((visibility("default"))) void UnityPluginLoad(void *interfaces) {
    (void)interfaces;
    if (!profiling_requested()) return;
    dl_iterate_phdr(find_mono, NULL);
    if (!mono_library) return;
    void *(*create)(void *) = dlsym(mono_library, "mono_profiler_create");
    int (*enable)(void) = dlsym(mono_library, "mono_profiler_enable_allocations");
    void (*set_callback)(void *, void (*)(void *, void *)) = dlsym(mono_library, "mono_profiler_set_gc_allocation_callback");
    object_size = dlsym(mono_library, "mono_object_get_size");
    if (!create || !enable || !set_callback || !object_size || !enable()) return;
    void *handle = create(NULL);
    if (!handle) return;
    set_callback(handle, allocation); allocation_available = 1;
}
__attribute__((visibility("default"))) int clustr_allocation_available(void) { return allocation_available; }
__attribute__((visibility("default"))) uint64_t clustr_allocated_bytes(void) {
    return atomic_load_explicit(&allocated_bytes, memory_order_relaxed);
}

// Sum requested buffer bytes for unique DRM clients. Shared backing may be counted
// conservatively across clients. Do not sum resident aliases with total fields.
__attribute__((visibility("default"))) int64_t clustr_gpu_memory_bytes(void) {
    DIR *directory = opendir("/proc/self/fdinfo");
    if (!directory) return -1;
    char clients[64][160]; unsigned client_count=0; uint64_t total=0;
    struct dirent *entry;
    while ((entry = readdir(directory))) {
        if (entry->d_name[0] == '.') continue;
        char *end; long descriptor = strtol(entry->d_name, &end, 10);
        struct stat descriptor_info;
        // Linux reserves character major 226 for DRM. Inspect every current fd,
        // including newly opened/reused descriptors, without reading unrelated fdinfo.
        if (*end || descriptor < 0 || descriptor > INT_MAX || fstat((int)descriptor, &descriptor_info) != 0 ||
            !S_ISCHR(descriptor_info.st_mode) || major(descriptor_info.st_rdev) != 226) continue;
        char path[512]; snprintf(path,sizeof(path),"/proc/self/fdinfo/%s",entry->d_name);
        FILE *file=fopen(path,"r"); if (!file) continue;
        char line[1024], pdev[80]="", client[80]="";
        uint64_t bytes=0; int has_memory=0;
        while (fgets(line,sizeof(line),file)) {
            if (sscanf(line,"drm-pdev: %79s",pdev)==1) continue;
            if (sscanf(line,"drm-client-id: %79s",client)==1) continue;
            if (!strncmp(line,"drm-total-",10) && strncmp(line,"drm-total-cycles-",17)) {
                char key[80],unit[16]=""; unsigned long long value;
                int fields=sscanf(line,"%79s %llu %15s",key,&value,unit);
                if (fields<2) { fclose(file); closedir(directory); return -1; }
                uint64_t multiplier=1;
                if (fields==3) {
                    if (!strcmp(unit,"KiB")) multiplier=1024;
                    else if (!strcmp(unit,"MiB")) multiplier=1024*1024;
                    else if (strcmp(unit,"B")) { fclose(file); closedir(directory); return -1; }
                }
                if (value > (UINT64_MAX-bytes)/multiplier) { fclose(file); closedir(directory); return -1; }
                bytes+=(uint64_t)value*multiplier; has_memory=1;
            }
        }
        fclose(file);
        if (!client[0] || !has_memory) continue;
        char identity[160]; snprintf(identity,sizeof(identity),"%s/%s",pdev,client);
        unsigned i; for (i=0;i<client_count;i++) if (!strcmp(clients[i],identity)) break;
        if (i<client_count) continue;
        if (client_count==64 || bytes > INT64_MAX || total > (uint64_t)INT64_MAX-bytes) { closedir(directory); return -1; }
        strcpy(clients[client_count++],identity); total+=bytes;
    }
    closedir(directory);
    return client_count ? (int64_t)total : -1;
}

// Linux getrusage(2) reports the process high-water RSS in KiB, including native allocations.
__attribute__((visibility("default"))) int64_t clustr_process_peak_rss_bytes(void) {
    struct rusage usage;
    if (getrusage(RUSAGE_SELF, &usage) != 0 || usage.ru_maxrss <= 0 ||
        (uint64_t)usage.ru_maxrss > INT64_MAX / 1024) return -1;
    return (int64_t)usage.ru_maxrss * 1024;
}

// Diagnostic execution counters, sampled at Update boundaries without heap allocation.
struct clustr_execution_sample {
    int64_t thread_cpu_ns, process_cpu_ns, voluntary_switches, involuntary_switches, major_faults;
};
__attribute__((visibility("default"))) int clustr_execution_snapshot(struct clustr_execution_sample *sample) {
    struct rusage thread, process;
    if (!sample || getrusage(RUSAGE_THREAD, &thread) != 0 || getrusage(RUSAGE_SELF, &process) != 0) return 0;
    sample->thread_cpu_ns = ((int64_t)thread.ru_utime.tv_sec + thread.ru_stime.tv_sec) * 1000000000 +
        ((int64_t)thread.ru_utime.tv_usec + thread.ru_stime.tv_usec) * 1000;
    sample->process_cpu_ns = ((int64_t)process.ru_utime.tv_sec + process.ru_stime.tv_sec) * 1000000000 +
        ((int64_t)process.ru_utime.tv_usec + process.ru_stime.tv_usec) * 1000;
    sample->voluntary_switches = thread.ru_nvcsw;
    sample->involuntary_switches = thread.ru_nivcsw;
    sample->major_faults = process.ru_majflt;
    return 1;
}
