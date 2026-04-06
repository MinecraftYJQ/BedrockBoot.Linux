#!/bin/bash

set -e
# 强制默认掩码
umask 022

# ========== 配置区域 ==========
PROJECT_NAME="BedrockBoot.Linux"
MAIN_PROJECT="BedrockBoot.Linux.Console"
OUTPUT_DIR="$(pwd)/publish"
# 关键修复：如果当前目录不支持权限，则在 /tmp 下创建构建根目录
BUILD_ROOT="/tmp/bbl-build-$(date +%s)"
DEB_ROOT="${BUILD_ROOT}/deb"
ALIAS_NAME="bbl"
ARCH="amd64"

VERSION_FORMAT="${VERSION_FORMAT:-standard}"

# ========== 颜色输出 ==========
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

print_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
print_error() { echo -e "${RED}[ERROR]${NC} $1"; }
print_step() { echo -e "${BLUE}[步骤]${NC} $1"; }

# ========== 版本号生成 ==========
generate_version() {
    echo "2.26.0.$(date +%Y%m%d%H%M%S)"
}

# ========== 依赖检查 ==========
check_dependencies() {
    print_step "检查构建依赖..."
    for cmd in dotnet dpkg-deb fakeroot; do
        if ! command -v $cmd &> /dev/null; then
            print_error "缺少依赖: $cmd (请运行: sudo apt install dotnet-sdk-10.0 dpkg-dev fakeroot)"
            exit 1
        fi
    done
}

# ========== 构建项目 ==========
build_project() {
    print_step "构建 .NET 项目..."
    # 清理并发布到本地 publish 目录
    rm -rf "${OUTPUT_DIR}"
    dotnet publish "${MAIN_PROJECT}" \
        -c Release \
        -o "${OUTPUT_DIR}" \
        --self-contained true \
        --runtime linux-x64 \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=false \
        -p:InformationalVersion="${VERSION}"
    
    chmod +x "${OUTPUT_DIR}/${MAIN_PROJECT}"
}

# ========== 准备 deb 包 ==========
prepare_deb() {
    print_step "在安全目录准备 deb 内容 (路径: ${DEB_ROOT})..."
    
    # 确保构建目录干净且在 Linux 原生分区(/tmp)
    rm -rf "${BUILD_ROOT}"
    mkdir -p "${DEB_ROOT}/DEBIAN"
    mkdir -p "${DEB_ROOT}/usr/local/bin"
    mkdir -p "${DEB_ROOT}/usr/share/bedrockboot"

    # 1. 复制二进制文件
    cp "${OUTPUT_DIR}/${MAIN_PROJECT}" "${DEB_ROOT}/usr/local/bin/${PROJECT_NAME}"
    ln -sf "${PROJECT_NAME}" "${DEB_ROOT}/usr/local/bin/${ALIAS_NAME}"
    
    # 2. 生成 Control 文件
    INSTALLED_SIZE=$(du -sk "${DEB_ROOT}" | cut -f1)
    cat > "${DEB_ROOT}/DEBIAN/control" << EOF
Package: bedrockboot-linux
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: ${ARCH}
Maintainer: BedrockBoot Team <support@bedrockboot.com>
Description: BedrockBoot Linux Client
 Installed-Size: ${INSTALLED_SIZE}
 Depends: libc6 (>= 2.35), libgcc-s1 (>= 12), libstdc++6 (>= 12)
EOF

    # 3. 生成维护脚本
    cat > "${DEB_ROOT}/DEBIAN/postinst" << 'EOF'
#!/bin/bash
set -e
ln -sf /usr/local/bin/BedrockBoot.Linux /usr/local/bin/bbl
exit 0
EOF
    cat > "${DEB_ROOT}/DEBIAN/prerm" << 'EOF'
#!/bin/bash
set -e
rm -f /usr/local/bin/bbl
exit 0
EOF

    # 4. 强制修正权限 (因为在 /tmp 下，chmod 必定有效)
    find "${DEB_ROOT}" -type d -exec chmod 755 {} +
    find "${DEB_ROOT}" -type f -exec chmod 644 {} +
    chmod 755 "${DEB_ROOT}/DEBIAN/postinst" "${DEB_ROOT}/DEBIAN/prerm"
    chmod 755 "${DEB_ROOT}/usr/local/bin/${PROJECT_NAME}"
    
    print_info "权限修正完成。"
}

# ========== 打包 deb ==========
create_deb() {
    print_step "打包成 deb 文件..."
    DEB_NAME="$(pwd)/${PROJECT_NAME}_${VERSION}_${ARCH}.deb"
    
    # 使用 fakeroot 模拟 root 环境，并强制指定 root 属主
    fakeroot dpkg-deb --build --root-owner-group "${DEB_ROOT}" "${DEB_NAME}"
    
    print_info "Deb 包创建成功: ${DEB_NAME}"
    # 清理临时构建目录
    rm -rf "${BUILD_ROOT}"
}

# ========== 主函数 ==========
main() {
    VERSION=$(generate_version)
    check_dependencies
    build_project
    prepare_deb
    create_deb
    
    echo -e "\n${GREEN}构建成功！${NC}"
    echo "提示：如果之前在 NTFS 分区报错，现在的包已在内存/临时目录中修复权限并导出。"
}

# 运行
case "${1:-build}" in
    build) main ;;
    clean) rm -rf publish *.deb "${BUILD_ROOT}" ;;
    *) echo "用法: $0 {build|clean}" ;;
esac