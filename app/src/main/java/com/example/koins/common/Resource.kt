package com.example.koins.common

import android.util.Log
import retrofit2.Response


const val TAG = "Resource"

sealed class Resource<T>(val data : T? = null, val message : String? = null) {
    class Idle<T>() : Resource<T>()
    class Loading<T>() : Resource<T>()
    class Success<T>(data: T) : Resource<T>(data = data)
    class Error<T>(message: String, data : T? = null) : Resource<T>(message = message, data = data)
}

fun <T> handleResource(res : Response<T>, message : String) : Resource<T>{
    if(res.isSuccessful){
        res.body()?.let {
            Log.d(TAG, "handleResource: Success in req for $message")
            return Resource.Success(data = it)
        }
    }
    Log.d(TAG, "handleResource: Error in req for $message")
    return Resource.Error(res.message())
}