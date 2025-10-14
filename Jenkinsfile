pipeline{
    agent{
        label "android-agent"
    }
    parameters{
        string(
            name: 'BRANCH',
            defaultValue: 'master',
            description: 'Git Branch'
        )
        string(
            name: 'BUILD_TYPE',
            defaultValue: 'debug',
            description: 'Build variant to build'
        )
    }

    environment{
        GIT_REPO_URL = 'https://github.com/abhi10601060/Koins.git'
        GIT_BRANCH = 'master'
        KEYSTORE = credentials('koins-key')
        KEYSTORE_PASS = credentials('keystore-password')
        KEY_ALIAS = credentials('koins-key-alias')
        KEY_ALIAS_PASS = credentials('keystore-password')
    }

    stages{

        stage("checkout"){
            steps{
                script{
                    echo "Starting build for Branch - ${params.BRANCH} & Build Variant - ${params.BUILD_TYPE}"
                    echo "clearing the workspace..."
                    deleteDir()

                    echo "Fetching Code from github..."
                    sh '''
                        git clone $GIT_REPO_URL
                        cd Koins
                        git fetch
                        git checkout master
                    '''
                }
            }
        }

        stage("Build") {
            steps{
                script{
                    sh '''
                        pwd
                        ls
                        cd Koins
                        ./gradlew clean
                        ./gradlew assembledebug \
                        -Pandroid.injected.signing.store.file=$KEYSTORE \
                        -Pandroid.injected.signing.store.password=$KEYSTORE_PASS \
                        -Pandroid.injected.signing.key.alias=$KEY_ALIAS \
                        -Pandroid.injected.signing.key.password=$KEY_ALIAS_PASS
                    '''
                }
            }
        }
    }
}