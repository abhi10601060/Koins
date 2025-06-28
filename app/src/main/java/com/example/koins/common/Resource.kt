package com.example.koins.common

sealed class Resource<T>(val data : T? = null, val message : String? = null) {
    class Idle<T>() : Resource<T>()
    class Loading<T>() : Resource<T>()
    class Success<T>(data: T) : Resource<T>(data = data)
    class Error<T>(message: String, data : T? = null) : Resource<T>(message = message, data = data)
}

fun <T> handleResource(res : Resource<T>, message : String) : Resource<T>{
    return  res
}